using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public sealed class ByRefParameterOverloadProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "ByRef Parameter Overloads";
    public override string Id => "byref_parameter_overloads";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");
        var byReference_CopyFrom = byReference.GetMethodByName(nameof(ByReference<>.CopyFrom));
        var byReference_CopyTo = byReference.GetMethodByName(nameof(ByReference<>.CopyTo));
        var byReference_Clear = byReference.GetMethodByName(nameof(ByReference<>.Clear));

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppType));
        var il2CppTypeHelper_SizeOf = il2CppTypeHelper.GetMethodByName(nameof(Il2CppType.SizeOf));

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                if (type.IsInterface)
                {
                    continue; // We don't add method overloads to interfaces
                }

                // for instead of foreach because we might be modifying the collection
                for (var methodIndex = 0; methodIndex < type.Methods.Count; methodIndex++)
                {
                    var method = type.Methods[methodIndex];
                    if (method.IsInjected || !method.IsPublic || method.IsSpecialName)
                        continue;

                    if (!method.Parameters.Any(p => p.DefaultParameterType is ByRefTypeAnalysisContext))
                        continue;

                    var newMethod = new InjectedMethodAnalysisContext(type, method.Name, appContext.SystemTypes.SystemVoidType, method.Attributes, [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(newMethod);

                    Debug.Assert(method.MostUserFriendlyOverload == method);
                    method.MostUserFriendlyOverload = newMethod;

                    newMethod.CopyGenericParameters(method, true);

                    var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, newMethod);

                    newMethod.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                    foreach (var parameter in method.Parameters)
                    {
                        TypeAnalysisContext parameterType;
                        if (parameter.DefaultParameterType is ByRefTypeAnalysisContext)
                        {
                            Debug.Assert(parameter.ParameterType is GenericInstanceTypeAnalysisContext { GenericArguments.Count: 1 });
                            var underlyingType = ((GenericInstanceTypeAnalysisContext)parameter.ParameterType).GenericArguments[0];
                            parameterType = visitor.Replace(underlyingType).MakeByReferenceType();
                        }
                        else
                        {
                            parameterType = visitor.Replace(parameter.ParameterType);
                        }

                        var newParameter = new InjectedParameterAnalysisContext(parameter.Name, parameterType, parameter.Attributes, parameter.ParameterIndex, newMethod);
                        newMethod.Parameters.Add(newParameter);
                    }

                    List<Instruction> instructions = new();
                    List<LocalVariable> variables = new();

                    LocalVariable?[] variableMap = new LocalVariable?[newMethod.Parameters.Count];

                    for (var i = 0; i < newMethod.Parameters.Count; i++)
                    {
                        var parameter = newMethod.Parameters[i];
                        if (parameter.ParameterType is ByRefTypeAnalysisContext { ElementType: { } underlyingType })
                        {
                            LocalVariable local = new(byReference.MakeGenericInstanceType([underlyingType]));
                            variables.Add(local);

                            instructions.Add(new Instruction(CilOpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(underlyingType)));
                            instructions.Add(new Instruction(CilOpCodes.Conv_U));
                            instructions.Add(new Instruction(CilOpCodes.Localloc));
                            instructions.Add(new Instruction(CilOpCodes.Newobj, new ConcreteGenericMethodAnalysisContext(byReference_Constructor, [underlyingType], [])));
                            instructions.Add(new Instruction(CilOpCodes.Stloc, local));

                            if (parameter.Attributes.HasFlag(ParameterAttributes.Out))
                            {
                                instructions.Add(new Instruction(CilOpCodes.Ldloca, local));
                                instructions.Add(new Instruction(CilOpCodes.Call, new ConcreteGenericMethodAnalysisContext(byReference_Clear, [underlyingType], [])));
                            }
                            else
                            {
                                instructions.Add(new Instruction(CilOpCodes.Ldloca, local));
                                instructions.Add(new Instruction(CilOpCodes.Ldarg, parameter));
                                instructions.Add(new Instruction(CilOpCodes.Call, new ConcreteGenericMethodAnalysisContext(byReference_CopyFrom, [underlyingType], [])));
                            }

                            variableMap[i] = local;
                        }
                    }

                    if (!newMethod.IsStatic)
                    {
                        instructions.Add(new Instruction(CilOpCodes.Ldarg, This.Instance));
                    }

                    for (var i = 0; i < newMethod.Parameters.Count; i++)
                    {
                        var local = variableMap[i];
                        if (local is not null)
                        {
                            instructions.Add(new Instruction(CilOpCodes.Ldloc, local));
                        }
                        else
                        {
                            instructions.Add(new Instruction(CilOpCodes.Ldarg, newMethod.Parameters[i]));
                        }
                    }

                    instructions.Add(new Instruction(newMethod.IsStatic || type.IsValueType ? CilOpCodes.Call : CilOpCodes.Callvirt, method.MaybeMakeConcreteGeneric(type.GenericParameters, newMethod.GenericParameters)));

                    LocalVariable? resultLocal;
                    if (newMethod.IsVoid)
                    {
                        resultLocal = null;
                    }
                    else
                    {
                        resultLocal = new(newMethod.ReturnType);
                        variables.Add(resultLocal);
                        instructions.Add(new Instruction(CilOpCodes.Stloc, resultLocal));
                    }

                    for (var i = 0; i < newMethod.Parameters.Count; i++)
                    {
                        var local = variableMap[i];
                        if (local is null)
                        {
                            continue;
                        }

                        var parameter = newMethod.Parameters[i];
                        if (parameter.Attributes.HasFlag(ParameterAttributes.In))
                        {
                            continue;
                        }

                        var underlyingType = ((ByRefTypeAnalysisContext)parameter.ParameterType).ElementType;

                        instructions.Add(new Instruction(CilOpCodes.Ldloca, local));
                        instructions.Add(new Instruction(CilOpCodes.Ldarg, parameter));
                        instructions.Add(new Instruction(CilOpCodes.Call, new ConcreteGenericMethodAnalysisContext(byReference_CopyTo, [underlyingType], [])));
                    }

                    if (resultLocal is not null)
                    {
                        instructions.Add(new Instruction(CilOpCodes.Ldloc, resultLocal));
                    }

                    instructions.Add(new Instruction(CilOpCodes.Ret));

                    newMethod.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = instructions,
                        LocalVariables = variables,
                    });
                }
            }
        }
    }
}
