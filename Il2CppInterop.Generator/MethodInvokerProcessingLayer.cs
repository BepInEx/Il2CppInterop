using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;
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

                    var redirectedType = instantiatedType.KnownType switch
                    {
                        _ when method.IsInstanceConstructor => instantiatedType,
                        KnownTypeCode.Il2CppSystem_Object => appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IObject"),
                        KnownTypeCode.Il2CppSystem_Enum => appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IEnum"),
                        KnownTypeCode.Il2CppSystem_ValueType => appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IValueType"),
                        _ => instantiatedType,
                    };

                    InjectedMethodAnalysisContext implementationMethod;
                    {
                        var name = GetNonConflictingName(method.IsInstanceConstructor ? "UnsafeConstructor" : $"UnsafeImplementation_{method.Name.Replace('.', '_')}", existingNames);

                        implementationMethod = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            type, // Placeholder return type
                            MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(implementationMethod);

                        method.UnsafeImplementationMethod = implementationMethod;

                        implementationMethod.CopyGenericParameters(method, true, true);

                        var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, implementationMethod);

                        implementationMethod.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                        if (!method.IsStatic)
                        {
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                byReference.MakeGenericInstanceType([redirectedType]),
                                ParameterAttributes.None,
                                0,
                                implementationMethod);
                            implementationMethod.Parameters.Add(newParameter);
                        }

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = byReference.MakeGenericInstanceType([visitor.Replace(originalParameter.ParameterType)]);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                implementationMethod.Parameters.Count,
                                implementationMethod);
                            implementationMethod.Parameters.Add(newParameter);
                        }
                    }

                    InjectedMethodAnalysisContext? valueTypeHelper = null;
                    InjectedMethodAnalysisContext? referenceTypeHelper = null;
                    if (method.IsStatic)
                    {
                        // No helper needed
                    }
                    else
                    {
                        var name = method.IsInstanceConstructor
                            ? GetNonConflictingName("UnsafeConstruct", existingNames)
                            : GetNonConflictingName($"UnsafeInvoke_{method.Name.MakeValidCSharpName()}", existingNames);

                        var helper = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            type, // Placeholder return type
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(helper);

                        method.UnsafeInvokeMethod = helper;

                        helper.CopyGenericParameters(method, true, true);

                        var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, helper);

                        helper.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                        // "this" parameter
                        if (type.IsValueType)
                        {
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                byReference.MakeGenericInstanceType([redirectedType]),
                                ParameterAttributes.None,
                                0,
                                helper);
                            helper.Parameters.Add(newParameter);
                        }
                        else
                        {
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                redirectedType,
                                ParameterAttributes.None,
                                0,
                                helper);
                            helper.Parameters.Add(newParameter);
                        }

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = visitor.Replace(originalParameter.ParameterType);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                helper.Parameters.Count,
                                helper);
                            helper.Parameters.Add(newParameter);
                        }

                        List<Instruction> instructions = new();

                        var thisIsLocal = !type.IsValueType;
                        var localVariablesOffset = thisIsLocal ? 0 : 1;
                        var localVariablesCount = thisIsLocal ? method.Parameters.Count + 1 : method.Parameters.Count;
                        LocalVariable[] localVariables = new LocalVariable[localVariablesCount];

                        for (var i = localVariablesOffset; i < helper.Parameters.Count; i++)
                        {
                            var parameter = helper.Parameters[i];
                            var parameterLocal = new LocalVariable(byReference.MakeGenericInstanceType([parameter.ParameterType]));
                            localVariables[i - localVariablesOffset] = parameterLocal;

                            instructions.Add(CilOpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(parameter.ParameterType));
                            instructions.Add(CilOpCodes.Conv_U);
                            instructions.Add(CilOpCodes.Localloc);
                            instructions.Add(CilOpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([parameter.ParameterType], []));
                            instructions.Add(CilOpCodes.Stloc, parameterLocal);

                            instructions.Add(CilOpCodes.Ldloca, parameterLocal);
                            instructions.Add(CilOpCodes.Ldarg, parameter);
                            instructions.Add(CilOpCodes.Call, byReference_SetValue.MakeConcreteGeneric([parameter.ParameterType], []));
                        }

                        if (!thisIsLocal)
                        {
                            instructions.Add(CilOpCodes.Ldarg, helper.Parameters[0]);
                        }

                        foreach (var parameterLocal in localVariables)
                        {
                            instructions.Add(CilOpCodes.Ldloc, parameterLocal);
                        }

                        instructions.Add(CilOpCodes.Call, implementationMethod.MaybeMakeConcreteGeneric(type.GenericParameters, helper.GenericParameters));

                        instructions.Add(CilOpCodes.Ret);

                        helper.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = localVariables,
                        });

                        if (type.IsValueType)
                        {
                            valueTypeHelper = helper;
                        }
                        else
                        {
                            referenceTypeHelper = helper;
                        }
                    }

                    // Method body
                    Debug.Assert(!method.HasExtraData<NativeMethodBody>());
                    if (valueTypeHelper is not null)
                    {
                        Debug.Assert(!method.IsStatic);
                        Debug.Assert(type.IsValueType);

                        List<Instruction> instructions = new();

                        LocalVariable instanceLocal = new(byReference.MakeGenericInstanceType([instantiatedType]));

                        instructions.Add(CilOpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(instantiatedType));
                        instructions.Add(CilOpCodes.Conv_U);
                        instructions.Add(CilOpCodes.Localloc);
                        instructions.Add(CilOpCodes.Newobj, new ConcreteGenericMethodAnalysisContext(byReference_Constructor, [instantiatedType], []));
                        instructions.Add(CilOpCodes.Stloc, instanceLocal);

                        instructions.Add(CilOpCodes.Ldloca, instanceLocal);
                        instructions.Add(CilOpCodes.Ldarg, This.Instance);
                        instructions.Add(CilOpCodes.Call, byReference_CopyFrom.MakeConcreteGeneric([instantiatedType], []));

                        instructions.Add(CilOpCodes.Ldloc, instanceLocal);

                        foreach (var parameter in method.Parameters)
                        {
                            instructions.Add(CilOpCodes.Ldarg, parameter);
                        }

                        instructions.Add(CilOpCodes.Call, valueTypeHelper.MaybeMakeConcreteGeneric(type.GenericParameters, method.GenericParameters));

                        instructions.Add(CilOpCodes.Ldloca, instanceLocal);
                        instructions.Add(CilOpCodes.Ldarg, This.Instance);
                        instructions.Add(CilOpCodes.Call, byReference_CopyTo.MakeConcreteGeneric([instantiatedType], []));

                        instructions.Add(CilOpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [instanceLocal],
                        });
                    }
                    else if (referenceTypeHelper is not null)
                    {
                        Debug.Assert(!method.IsStatic);
                        Debug.Assert(!type.IsValueType);

                        List<Instruction> instructions = [];

                        if (method.IsInstanceConstructor)
                        {
                            Debug.Assert(type.PointerConstructor is not null);
                            instructions.Add(CilOpCodes.Ldarg, This.Instance);
                            instructions.Add(CilOpCodes.Call, newObjectPointer.MakeGenericInstanceMethod(instantiatedType));
                            instructions.Add(CilOpCodes.Call, type.PointerConstructor!.MaybeMakeConcreteGeneric(type.GenericParameters, []));
                        }

                        instructions.Add(CilOpCodes.Ldarg, This.Instance);
                        foreach (var parameter in method.Parameters)
                        {
                            instructions.Add(CilOpCodes.Ldarg, parameter);
                        }
                        instructions.Add(CilOpCodes.Call, referenceTypeHelper.MaybeMakeConcreteGeneric(type.GenericParameters, method.GenericParameters));
                        instructions.Add(CilOpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [],
                        });
                    }
                    else
                    {
                        Debug.Assert(method.IsStatic);

                        List<Instruction> instructions = new();

                        LocalVariable[] parameterLocals = new LocalVariable[method.Parameters.Count];

                        for (var i = 0; i < method.Parameters.Count; i++)
                        {
                            var parameter = method.Parameters[i];
                            var parameterLocal = new LocalVariable(byReference.MakeGenericInstanceType([parameter.ParameterType]));
                            parameterLocals[i] = parameterLocal;

                            instructions.Add(CilOpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(parameter.ParameterType));
                            instructions.Add(CilOpCodes.Conv_U);
                            instructions.Add(CilOpCodes.Localloc);
                            instructions.Add(CilOpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([parameter.ParameterType], []));
                            instructions.Add(CilOpCodes.Stloc, parameterLocal);

                            instructions.Add(CilOpCodes.Ldloca, parameterLocal);
                            instructions.Add(CilOpCodes.Ldarg, parameter);
                            instructions.Add(CilOpCodes.Call, byReference_SetValue.MakeConcreteGeneric([parameter.ParameterType], []));
                        }

                        foreach (var parameterLocal in parameterLocals)
                        {
                            instructions.Add(CilOpCodes.Ldloc, parameterLocal);
                        }

                        instructions.Add(CilOpCodes.Call, implementationMethod.MaybeMakeConcreteGeneric(type.GenericParameters, method.GenericParameters));

                        instructions.Add(CilOpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = parameterLocals,
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
