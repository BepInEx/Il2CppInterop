using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

public sealed class UserFriendlyOverloadProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "User-Friendly Overloads";
    public override string Id => "user_friendly_overloads";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        const string ArrayNamespace = "Il2CppInterop.Runtime.InteropTypes.Arrays";
        const string ArrayClassName = nameof(Il2CppArrayBase<>) + "`1";

        var il2CppArrayBase = appContext.ResolveTypeOrThrow(typeof(Il2CppArrayBase<>));
        var il2CppArrayBase_ToManagedArray = il2CppArrayBase.Methods.Single(m => m.Name == "op_Explicit" && m.ReturnType is SzArrayTypeAnalysisContext);
        var il2CppArrayBase_FromManagedArray = il2CppArrayBase.Methods.Single(m => m.Name == "op_Explicit" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType is SzArrayTypeAnalysisContext);

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

                    method = method.MostUserFriendlyOverload;

                    var anyPossibleConversions = method.Parameters.Any(p =>
                    {
                        // Convert Il2CppArrayBase<T> to T[]
                        if (p.ParameterType is GenericInstanceTypeAnalysisContext { GenericType: { Namespace: ArrayNamespace, Name: ArrayClassName } })
                            return true;

                        // Convert Il2Cpp delegate type to System delegate type
                        // Not implemented yet

                        // Convert ref Il2CppSystem.Int32 to ref int
                        // Not implemented yet

                        // Convert Il2Cpp primitive to System primitive
                        // Not implemented yet

                        return false;
                    });
                    if (!anyPossibleConversions)
                        continue;

                    var newMethod = new InjectedMethodAnalysisContext(type, method.Name, appContext.SystemTypes.SystemVoidType, method.Attributes, [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(newMethod);

                    type.Methods[methodIndex].MostUserFriendlyOverload = newMethod;

                    foreach (var gp in method.GenericParameters)
                    {
                        newMethod.GenericParameters.Add(new GenericParameterTypeAnalysisContext(gp.Name, gp.Index, gp.Type, gp.Attributes, newMethod));
                    }

                    TypeReplacementVisitor visitor = new(Enumerable.Range(0, method.GenericParameters.Count).ToDictionary<int, TypeAnalysisContext, TypeAnalysisContext>(i => method.GenericParameters[i], i => newMethod.GenericParameters[i]));

                    for (var i = 0; i < method.GenericParameters.Count; i++)
                    {
                        var originalGp = method.GenericParameters[i];
                        var newGp = newMethod.GenericParameters[i];
                        foreach (var constraint in originalGp.ConstraintTypes)
                        {
                            newGp.ConstraintTypes.Add(visitor.Replace(constraint));
                        }
                    }

                    TypeAnalysisContext[] parameterTypes = new TypeAnalysisContext[method.Parameters.Count];
                    MethodAnalysisContext?[] conversionMethods = new MethodAnalysisContext?[method.Parameters.Count];

                    for (var i = 0; i < method.Parameters.Count; i++)
                    {
                        var parameter = method.Parameters[i];

                        // Convert Il2CppArrayBase<T> to T[]
                        if (parameter.ParameterType is GenericInstanceTypeAnalysisContext { GenericType: { Namespace: ArrayNamespace, Name: ArrayClassName }, GenericArguments: [var elementType] })
                        {
                            parameterTypes[i] = visitor.Replace(elementType).MakeSzArrayType();
                            conversionMethods[i] = il2CppArrayBase_FromManagedArray.MakeConcreteGeneric([elementType], []);
                            continue;
                        }

                        // No conversion
                        {
                            parameterTypes[i] = visitor.Replace(parameter.ParameterType);
                        }
                    }

                    newMethod.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                    for (var i = 0; i < method.Parameters.Count; i++)
                    {
                        var parameter = method.Parameters[i];

                        var newParameter = new InjectedParameterAnalysisContext(parameter.Name, parameterTypes[i], parameter.Attributes, parameter.ParameterIndex, newMethod);
                        newMethod.Parameters.Add(newParameter);
                    }

                    List<Instruction> instructions = new();

                    if (!newMethod.IsStatic)
                    {
                        instructions.Add(new Instruction(OpCodes.Ldarg, This.Instance));
                    }

                    for (var i = 0; i < newMethod.Parameters.Count; i++)
                    {
                        instructions.Add(new Instruction(OpCodes.Ldarg, newMethod.Parameters[i]));
                        var conversionMethod = conversionMethods[i];
                        if (conversionMethod is not null)
                        {
                            instructions.Add(new Instruction(OpCodes.Call, conversionMethod));
                        }
                    }

                    instructions.Add(new Instruction(newMethod.IsStatic ? OpCodes.Call : OpCodes.Callvirt, method.MaybeMakeConcreteGeneric(type.GenericParameters, newMethod.GenericParameters)));

                    instructions.Add(new Instruction(OpCodes.Ret));

                    newMethod.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = instructions,
                    });
                }
            }
        }
    }
}
