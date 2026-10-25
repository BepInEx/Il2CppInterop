using System.Reflection;
using System.Runtime.CompilerServices;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using CilOpCodes = AsmResolver.PE.DotNet.Cil.CilOpCodes;

namespace Il2CppInterop.Generator;

public sealed class UserFriendlyOverloadProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "User-Friendly Overloads";
    public override string Id => "user_friendly_overloads";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        const string ArrayNamespace = "Il2CppInterop.Runtime.InteropTypes.Arrays";
        const string ArrayClassName = nameof(Il2CppArrayRank1<>) + "`1";

        var il2CppArrayBase = appContext.ResolveTypeOrThrow(typeof(Il2CppArrayRank1<>));
        var il2CppArrayBase_ToManagedArray = il2CppArrayBase.Methods.Single(m => m.Name == "op_Explicit" && m.ReturnType is SzArrayTypeAnalysisContext);
        var il2CppArrayBase_FromManagedArray = il2CppArrayBase.Methods.Single(m => m.Name == "op_Explicit" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType is SzArrayTypeAnalysisContext);

        var unsafeClass = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.Runtime.CompilerServices.Unsafe");
        var unsafeAsMethod = unsafeClass.Methods.Single(m => m.Name == nameof(Unsafe.As) && m.GenericParameters.Count == 2);

        var systemObject = appContext.SystemTypes.SystemObjectType;
        var il2CppSystemIObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IObject");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;
            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                // for instead of foreach because we might be modifying the collection
                for (var methodIndex = 0; methodIndex < type.Methods.Count; methodIndex++)
                {
                    var method = type.Methods[methodIndex];
                    if (method.IsInjected)
                        continue;
                    if (!method.IsPublic || method.IsSpecialName)
                        continue; // Don't generate overloads for non-public or special name methods
                    if (method.IsVirtual && !method.IsNewSlot)
                        continue; // Don't generate overloads for overrides/implementations

                    method = method.MostUserFriendlyOverload;

                    var anyPossibleConversions = method.Parameters.Any(p =>
                    {
                        // Convert Il2CppArrayBase<T> to T[]
                        if (p.ParameterType is GenericInstanceTypeAnalysisContext { GenericType: { Namespace: ArrayNamespace, Name: ArrayClassName } })
                            return true;

                        // Convert Il2Cpp delegate type to System delegate type
                        if (p.ParameterType.IsIl2CppDelegate)
                        {
                            // If GenericInstanceTypeAnalysisContext held an instantiated list of methods, this would be easier.
                            var nonGenericType = (p.ParameterType as GenericInstanceTypeAnalysisContext)?.GenericType ?? p.ParameterType;
                            return nonGenericType.Methods.Any(m => m.Name == "op_Explicit");
                        }

                        // Convert ref Il2CppSystem.Int32 to ref int
                        if (p.ParameterType is ByRefTypeAnalysisContext { ElementType.KnownType.IsIl2CppPrimitiveType: true })
                            return true;

                        // Convert Il2Cpp primitive to System primitive
                        // Although we include these conversions in the generated overload, they don't justify an overload on their own since implicit conversions exist.

                        return false;
                    });
                    if (!anyPossibleConversions)
                        continue;

                    const MethodAttributes AttributesToRemove = MethodAttributes.Virtual | MethodAttributes.Abstract | MethodAttributes.Final | MethodAttributes.NewSlot;
                    var newMethod = new InjectedMethodAnalysisContext(type, method.Name, appContext.SystemTypes.SystemVoidType, method.Attributes & ~AttributesToRemove, [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(newMethod);

                    // Have to use the index here because we overwrite the "method" local variable above
                    type.Methods[methodIndex].MostUserFriendlyOverload = newMethod;

                    newMethod.CopyGenericParameters(method, true);

                    var visitor = TypeReplacementVisitor.CreateForMethodCopying(method, newMethod);

                    TypeAnalysisContext[] parameterTypes = new TypeAnalysisContext[method.Parameters.Count];
                    MethodAnalysisContext?[] conversionMethods = new MethodAnalysisContext?[method.Parameters.Count];

                    var conversionCount = 0;
                    for (var i = 0; i < method.Parameters.Count; i++)
                    {
                        var parameter = method.Parameters[i];

                        // Change Il2CppArrayBase<T> to T[]
                        if (parameter.ParameterType is GenericInstanceTypeAnalysisContext { GenericType: { Namespace: ArrayNamespace, Name: ArrayClassName }, GenericArguments: [var elementType] })
                        {
                            parameterTypes[i] = visitor.Replace(elementType).MakeSzArrayType();
                            conversionMethods[i] = il2CppArrayBase_FromManagedArray.MakeConcreteGeneric([elementType], []);
                            conversionCount++;
                            continue;
                        }

                        // Change ref Il2CppSystem.Int32 to ref int
                        if (parameter.ParameterType is ByRefTypeAnalysisContext { ElementType: { KnownType.IsIl2CppPrimitiveType: true } byRefElementType })
                        {
                            var systemPrimitive = byRefElementType.KnownType.ToSystemType().ToContext(appContext);
                            parameterTypes[i] = systemPrimitive.MakeByReferenceType();
                            conversionMethods[i] = unsafeAsMethod.MakeGenericInstanceMethod(byRefElementType, systemPrimitive);
                            conversionCount++;
                            continue;
                        }

                        // Change Il2Cpp delegate type to System delegate type
                        if (parameter.ParameterType.IsIl2CppDelegate)
                        {
                            MethodAnalysisContext? explicitConversionMethod;
                            if (parameter.ParameterType is GenericInstanceTypeAnalysisContext genericInstance)
                            {
                                var managedDelegateType = genericInstance.GenericType.ManagedDelegateType;
                                explicitConversionMethod = genericInstance.GenericType.Methods.SingleOrDefault(m => m.Name == "op_Explicit" && m.Parameters[0].ParameterType == managedDelegateType)
                                    ?.MakeConcreteGeneric(genericInstance.GenericArguments, []);
                            }
                            else
                            {
                                var managedDelegateType = parameter.ParameterType.ManagedDelegateType;
                                explicitConversionMethod = parameter.ParameterType.Methods.SingleOrDefault(m => m.Name == "op_Explicit" && m.Parameters[0].ParameterType == managedDelegateType);
                            }
                            if (explicitConversionMethod is not null)
                            {
                                parameterTypes[i] = explicitConversionMethod.Parameters[0].ParameterType;
                                conversionMethods[i] = explicitConversionMethod;
                                conversionCount++;
                                continue;
                            }
                        }

                        // Change Il2Cpp primitive to System primitive
                        {
                            var knownType = parameter.ParameterType.KnownType;
                            if (knownType.IsIl2CppPrimitiveType || knownType is KnownTypeCode.Il2CppSystem_String)
                            {
                                var systemType = knownType.ToSystemType().ToContext(appContext);
                                parameterTypes[i] = systemType;
                                conversionMethods[i] = parameter.ParameterType.GetImplicitConversionFrom(systemType);
                                conversionCount++;
                                continue;
                            }
                            else if (knownType == KnownTypeCode.Il2CppSystem_IObject)
                            {
                                parameterTypes[i] = systemObject;
                                conversionCount++;
                                continue;
                                // No conversion method. We special case this in the loop below.
                            }
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

                    var instructionCount = (newMethod.IsStatic ? 0 : 1)
                        + newMethod.Parameters.Count
                        + conversionCount
                        + 2;

                    List<Instruction> instructions = new(instructionCount);

                    if (!newMethod.IsStatic)
                    {
                        instructions.Add(CilOpCodes.Ldarg_0); // Load "this"
                    }

                    for (var i = 0; i < newMethod.Parameters.Count; i++)
                    {
                        instructions.Add(CilOpCodes.Ldarg, newMethod.Parameters[i]);
                        var conversionMethod = conversionMethods[i];
                        if (conversionMethod is not null)
                        {
                            instructions.Add(CilOpCodes.Call, conversionMethod);
                        }
                        else if (newMethod.Parameters[i].ParameterType == systemObject && method.Parameters[i].ParameterType == il2CppSystemIObject)
                        {
                            // Special case for Il2CppSystem.IObject to System.Object conversion, since there's no conversion method for this
                            instructions.Add(CilOpCodes.Isinst, il2CppSystemIObject);
                        }
                    }

                    instructions.Add(newMethod.IsStatic || type.IsValueType ? CilOpCodes.Call : CilOpCodes.Callvirt, method.MaybeMakeConcreteGeneric(type.GenericParameters, newMethod.GenericParameters));

                    instructions.Add(CilOpCodes.Ret);

                    newMethod.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = instructions,
                    });
                }
            }
        }
    }
}
