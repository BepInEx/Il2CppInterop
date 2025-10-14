using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

// Todo: add attributes making these not show up in IntelliSense
public class MethodInvokerProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Method Invoker Processor";
    public override string Id => "method_invoker_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_GetValue = byReference.GetMethodByName(nameof(ByReference<>.GetValue));
        var byReference_SetValue = byReference.GetMethodByName(nameof(ByReference<>.SetValue));
        var byReference_CopyFrom = byReference.GetMethodByName(nameof(ByReference<>.CopyFrom));
        var byReference_CopyTo = byReference.GetMethodByName(nameof(ByReference<>.CopyTo));
        var byReference_Clear = byReference.GetMethodByName(nameof(ByReference<>.Clear));
        var byReference_ToPointer = byReference.GetMethodByName(nameof(ByReference<>.ToPointer));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper));
        var il2CppTypeHelper_SizeOf = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.SizeOf));

        var il2CppStaticClass = appContext.ResolveTypeOrThrow(typeof(IL2CPP));
        var newObjectPointer = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.NewObjectPointer));

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                var instantiatedType = type.SelfInstantiateIfGeneric();

                HashSet<string> existingNames = [type.CSharpName, .. type.Members.Select(m => m.Name)];

                for (var methodIndex = 0; methodIndex < type.Methods.Count; methodIndex++)
                {
                    var method = type.Methods[methodIndex];
                    if (method.IsInjected)
                        continue;

                    InjectedMethodAnalysisContext invoker;
                    {
                        var name = GetNonConflictingName(method.IsInstanceConstructor ? "UnsafeConstructor" : $"UnsafeImplementation_{method.Name.Replace('.', '_')}", existingNames);

                        invoker = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            type, // Placeholder return type
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(invoker);

                        method.UnsafeImplementationMethod = invoker;

                        foreach (var gp in method.GenericParameters)
                        {
                            invoker.GenericParameters.Add(new GenericParameterTypeAnalysisContext(gp.Name, gp.Index, gp.Type, gp.Attributes, invoker));
                        }

                        var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, invoker);

                        for (var i = 0; i < method.GenericParameters.Count; i++)
                        {
                            var originalGp = method.GenericParameters[i];
                            var newGp = invoker.GenericParameters[i];
                            foreach (var constraint in originalGp.ConstraintTypes)
                            {
                                newGp.ConstraintTypes.Add(visitor.Replace(constraint));
                            }
                        }

                        invoker.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                        if (!method.IsStatic)
                        {
                            var redirectedType = instantiatedType.KnownType switch
                            {
                                _ when method.IsInstanceConstructor => instantiatedType,
                                KnownTypeCode.Il2CppSystem_Object => appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IObject"),
                                KnownTypeCode.Il2CppSystem_Enum => appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IEnum"),
                                KnownTypeCode.Il2CppSystem_ValueType => appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IValueType"),
                                _ => instantiatedType,
                            };
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                byReference.MakeGenericInstanceType([redirectedType]),
                                ParameterAttributes.None,
                                0,
                                invoker);
                            invoker.Parameters.Add(newParameter);
                        }

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = byReference.MakeGenericInstanceType([visitor.Replace(originalParameter.ParameterType)]);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                invoker.Parameters.Count,
                                invoker);
                            invoker.Parameters.Add(newParameter);
                        }
                    }

                    InjectedMethodAnalysisContext? valueTypeHelper = null;
                    InjectedMethodAnalysisContext? constructorHelper = null;
                    if (method.IsStatic)
                    {
                        // No helper needed
                    }
                    else if (!method.IsInstanceConstructor && type.IsValueType)
                    {
                        var name = GetNonConflictingName($"UnsafeInvoke_{method.Name.Replace('.', '_')}", existingNames);

                        valueTypeHelper = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            type, // Placeholder return type
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(valueTypeHelper);

                        method.UnsafeInvokeMethod = valueTypeHelper;

                        foreach (var gp in method.GenericParameters)
                        {
                            valueTypeHelper.GenericParameters.Add(new GenericParameterTypeAnalysisContext(gp.Name, gp.Index, gp.Type, gp.Attributes, valueTypeHelper));
                        }

                        var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, valueTypeHelper);

                        for (var i = 0; i < method.GenericParameters.Count; i++)
                        {
                            var originalGp = method.GenericParameters[i];
                            var newGp = valueTypeHelper.GenericParameters[i];
                            foreach (var constraint in originalGp.ConstraintTypes)
                            {
                                newGp.ConstraintTypes.Add(visitor.Replace(constraint));
                            }
                        }

                        valueTypeHelper.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                        // "this" parameter
                        {
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                byReference.MakeGenericInstanceType([instantiatedType]),
                                ParameterAttributes.None,
                                0,
                                valueTypeHelper);
                            valueTypeHelper.Parameters.Add(newParameter);
                        }

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = visitor.Replace(originalParameter.ParameterType);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                valueTypeHelper.Parameters.Count,
                                valueTypeHelper);
                            valueTypeHelper.Parameters.Add(newParameter);
                        }

                        List<Instruction> instructions = new();

                        LocalVariable[] localVariables = new LocalVariable[method.Parameters.Count];

                        for (var i = 1; i < valueTypeHelper.Parameters.Count; i++)
                        {
                            var parameter = valueTypeHelper.Parameters[i];
                            var parameterLocal = new LocalVariable(byReference.MakeGenericInstanceType([parameter.ParameterType]));
                            localVariables[i - 1] = parameterLocal;

                            instructions.Add(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(parameter.ParameterType));
                            instructions.Add(OpCodes.Conv_U);
                            instructions.Add(OpCodes.Localloc);
                            instructions.Add(OpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([parameter.ParameterType], []));
                            instructions.Add(OpCodes.Stloc, parameterLocal);

                            instructions.Add(OpCodes.Ldloca, parameterLocal);
                            instructions.Add(OpCodes.Ldarg, parameter);
                            instructions.Add(OpCodes.Call, byReference_SetValue.MakeConcreteGeneric([parameter.ParameterType], []));
                        }

                        instructions.Add(OpCodes.Ldarg, valueTypeHelper.Parameters[0]);

                        foreach (var parameterLocal in localVariables)
                        {
                            instructions.Add(OpCodes.Ldloc, parameterLocal);
                        }

                        instructions.Add(OpCodes.Call, invoker.MaybeMakeConcreteGeneric(type.GenericParameters, []));

                        instructions.Add(OpCodes.Ret);

                        valueTypeHelper.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = localVariables,
                        });
                    }
                    else if (method.IsInstanceConstructor && !type.IsValueType)
                    {
                        var name = GetNonConflictingName("UnsafeConstruct", existingNames);

                        constructorHelper = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            type, // Placeholder return type
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(constructorHelper);

                        method.UnsafeInvokeMethod = constructorHelper;

                        foreach (var gp in method.GenericParameters)
                        {
                            constructorHelper.GenericParameters.Add(new GenericParameterTypeAnalysisContext(gp.Name, gp.Index, gp.Type, gp.Attributes, constructorHelper));
                        }

                        var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, constructorHelper);

                        for (var i = 0; i < method.GenericParameters.Count; i++)
                        {
                            var originalGp = method.GenericParameters[i];
                            var newGp = constructorHelper.GenericParameters[i];
                            foreach (var constraint in originalGp.ConstraintTypes)
                            {
                                newGp.ConstraintTypes.Add(visitor.Replace(constraint));
                            }
                        }

                        constructorHelper.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                        // "this" parameter
                        {
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                instantiatedType,
                                ParameterAttributes.None,
                                0,
                                constructorHelper);
                            constructorHelper.Parameters.Add(newParameter);
                        }

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = visitor.Replace(originalParameter.ParameterType);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                constructorHelper.Parameters.Count,
                                constructorHelper);
                            constructorHelper.Parameters.Add(newParameter);
                        }

                        List<Instruction> instructions = new();

                        LocalVariable[] localVariables = new LocalVariable[method.Parameters.Count + 1];

                        for (var i = 0; i < constructorHelper.Parameters.Count; i++)
                        {
                            var parameter = constructorHelper.Parameters[i];
                            var parameterLocal = new LocalVariable(byReference.MakeGenericInstanceType([parameter.ParameterType]));
                            localVariables[i] = parameterLocal;

                            instructions.Add(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(parameter.ParameterType));
                            instructions.Add(OpCodes.Conv_U);
                            instructions.Add(OpCodes.Localloc);
                            instructions.Add(OpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([parameter.ParameterType], []));
                            instructions.Add(OpCodes.Stloc, parameterLocal);

                            instructions.Add(OpCodes.Ldloca, parameterLocal);
                            instructions.Add(OpCodes.Ldarg, parameter);
                            instructions.Add(OpCodes.Call, byReference_SetValue.MakeConcreteGeneric([parameter.ParameterType], []));
                        }

                        foreach (var parameterLocal in localVariables)
                        {
                            instructions.Add(OpCodes.Ldloc, parameterLocal);
                        }

                        instructions.Add(OpCodes.Call, invoker.MaybeMakeConcreteGeneric(type.GenericParameters, []));

                        instructions.Add(OpCodes.Ret);

                        constructorHelper.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = localVariables,
                        });
                    }

                    // Method body
                    if (valueTypeHelper is not null)
                    {
                        Debug.Assert(!method.IsStatic);
                        Debug.Assert(!method.IsInstanceConstructor);
                        Debug.Assert(type.IsValueType);

                        List<Instruction> instructions = new();

                        LocalVariable instanceLocal = new(byReference.MakeGenericInstanceType([instantiatedType]));

                        instructions.Add(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(instantiatedType));
                        instructions.Add(OpCodes.Conv_U);
                        instructions.Add(OpCodes.Localloc);
                        instructions.Add(OpCodes.Newobj, new ConcreteGenericMethodAnalysisContext(byReference_Constructor, [instantiatedType], []));
                        instructions.Add(OpCodes.Stloc, instanceLocal);

                        instructions.Add(OpCodes.Ldloca, instanceLocal);
                        instructions.Add(OpCodes.Ldarg, This.Instance);
                        instructions.Add(OpCodes.Call, byReference_CopyFrom.MakeConcreteGeneric([instantiatedType], []));

                        instructions.Add(OpCodes.Ldloc, instanceLocal);

                        foreach (var parameter in method.Parameters)
                        {
                            instructions.Add(OpCodes.Ldarg, parameter);
                        }

                        instructions.Add(OpCodes.Call, valueTypeHelper.MaybeMakeConcreteGeneric(type.GenericParameters, []));

                        instructions.Add(OpCodes.Ldloca, instanceLocal);
                        instructions.Add(OpCodes.Ldarg, This.Instance);
                        instructions.Add(OpCodes.Call, byReference_CopyTo.MakeConcreteGeneric([instantiatedType], []));

                        instructions.Add(OpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [instanceLocal],
                        });
                    }
                    else if (constructorHelper is not null)
                    {
                        Debug.Assert(method.IsInstanceConstructor);
                        Debug.Assert(!type.IsValueType);
                        Debug.Assert(type.PointerConstructor is not null);

                        List<Instruction> instructions = [];

                        instructions.Add(OpCodes.Ldarg, This.Instance);
                        instructions.Add(OpCodes.Call, newObjectPointer.MakeGenericInstanceMethod(instantiatedType));
                        instructions.Add(OpCodes.Call, type.PointerConstructor!.MaybeMakeConcreteGeneric(type.GenericParameters, []));

                        instructions.Add(OpCodes.Ldarg, This.Instance);
                        foreach (var parameter in method.Parameters)
                        {
                            instructions.Add(OpCodes.Ldarg, parameter);
                        }
                        instructions.Add(OpCodes.Call, constructorHelper.MaybeMakeConcreteGeneric(type.GenericParameters, []));
                        instructions.Add(OpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [],
                        });
                    }
                    else
                    {
                        List<Instruction> instructions = new();

                        if (method.IsInstanceConstructor && !type.IsValueType)
                        {
                            Debug.Assert(type.PointerConstructor is not null);

                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Call, newObjectPointer.MakeGenericInstanceMethod(instantiatedType));
                            instructions.Add(OpCodes.Call, type.PointerConstructor!.MaybeMakeConcreteGeneric(type.GenericParameters, []));
                        }

                        LocalVariable? instanceLocal;
                        if (method.IsStatic)
                        {
                            instanceLocal = null;
                        }
                        else
                        {
                            var byRefType = invoker.Parameters[0].ParameterType;
                            var byRefElementType = ((GenericInstanceTypeAnalysisContext)byRefType).GenericArguments[0];

                            instanceLocal = new(byRefType);

                            instructions.Add(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(byRefElementType));
                            instructions.Add(OpCodes.Conv_U);
                            instructions.Add(OpCodes.Localloc);
                            instructions.Add(OpCodes.Newobj, new ConcreteGenericMethodAnalysisContext(byReference_Constructor, [byRefElementType], []));
                            instructions.Add(OpCodes.Stloc, instanceLocal);

                            if (type.IsValueType)
                            {
                                instructions.Add(OpCodes.Ldloca, instanceLocal);
                                instructions.Add(OpCodes.Ldarg, This.Instance);
                                instructions.Add(OpCodes.Call, byReference_CopyFrom.MakeConcreteGeneric([byRefElementType], []));
                            }
                            else
                            {
                                instructions.Add(OpCodes.Ldloca, instanceLocal);
                                instructions.Add(OpCodes.Ldarg, This.Instance);
                                instructions.Add(OpCodes.Call, byReference_SetValue.MakeConcreteGeneric([byRefElementType], []));
                            }
                        }

                        LocalVariable[] localVariables;
                        Span<LocalVariable> parameterLocals;
                        if (instanceLocal is not null)
                        {
                            localVariables = new LocalVariable[method.Parameters.Count + 1];
                            localVariables[0] = instanceLocal;
                            parameterLocals = localVariables.AsSpan(1);
                        }
                        else
                        {
                            localVariables = new LocalVariable[method.Parameters.Count];
                            parameterLocals = localVariables;
                        }

                        for (var i = 0; i < method.Parameters.Count; i++)
                        {
                            var parameter = method.Parameters[i];
                            var parameterLocal = new LocalVariable(byReference.MakeGenericInstanceType([parameter.ParameterType]));
                            parameterLocals[i] = parameterLocal;

                            instructions.Add(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(parameter.ParameterType));
                            instructions.Add(OpCodes.Conv_U);
                            instructions.Add(OpCodes.Localloc);
                            instructions.Add(OpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([parameter.ParameterType], []));
                            instructions.Add(OpCodes.Stloc, parameterLocal);

                            instructions.Add(OpCodes.Ldloca, parameterLocal);
                            instructions.Add(OpCodes.Ldarg, parameter);
                            instructions.Add(OpCodes.Call, byReference_SetValue.MakeConcreteGeneric([parameter.ParameterType], []));
                        }

                        if (instanceLocal is not null)
                        {
                            instructions.Add(OpCodes.Ldloc, instanceLocal);
                        }

                        foreach (var parameterLocal in parameterLocals)
                        {
                            instructions.Add(OpCodes.Ldloc, parameterLocal);
                        }

                        instructions.Add(OpCodes.Call, invoker.MaybeMakeConcreteGeneric(type.GenericParameters, []));

                        if (instanceLocal is not null && type.IsValueType)
                        {
                            instructions.Add(OpCodes.Ldloca, instanceLocal);
                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Call, byReference_CopyTo.MakeConcreteGeneric([instantiatedType], []));
                        }

                        instructions.Add(OpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = localVariables,
                        });
                    }

                    // Interface redirect body
                    if (method.InterfaceRedirectMethod is not null)
                    {
                        Debug.Assert(method.GenericParameters.Count == 0);
                        Debug.Assert(method.DeclaringType is { GenericParameters.Count: 0 });

                        var methodBody = method.GetExtraData<NativeMethodBody>();
                        Debug.Assert(methodBody is { ExceptionHandlers.Count: 0 });

                        var localVariables = new LocalVariable[methodBody.LocalVariables.Count];
                        var operandRedirects = new Dictionary<object, object>(methodBody.LocalVariables.Count);
                        for (var i = 0; i < localVariables.Length; i++)
                        {
                            var originalLocal = methodBody.LocalVariables[i];
                            var newLocal = new LocalVariable(originalLocal.Type);
                            localVariables[i] = newLocal;
                            operandRedirects.Add(originalLocal, newLocal);
                        }

                        method.InterfaceRedirectMethod!.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = methodBody.Instructions.Select(i =>
                            {
                                return new Instruction(i.Code, i.Operand is not null && operandRedirects.TryGetValue(i.Operand, out var redirectedOperand) ? redirectedOperand : i.Operand);
                            }).ToArray(),
                            LocalVariables = localVariables,
                        });

                        if (method.UnsafeInvokeMethod is not null)
                        {
                            method.InterfaceRedirectMethod!.UnsafeInvokeMethod = method.UnsafeInvokeMethod!;
                        }

                        if (method.UnsafeImplementationMethod is not null)
                        {
                            method.InterfaceRedirectMethod!.UnsafeImplementationMethod = method.UnsafeImplementationMethod!;
                        }
                    }
                }
            }
        }
    }

    private static string GetNonConflictingName(string baseName, HashSet<string> existingNames)
    {
        var name = baseName;
        while (existingNames.Contains(name))
        {
            name = $"{baseName}_";
        }
        return name;
    }
}
