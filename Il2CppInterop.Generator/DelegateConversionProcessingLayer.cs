using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator;

public class DelegateConversionProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Delegate Conversion";

    public override string Id => "delegate_conversion";

    /// <summary>
    /// The maximum number of parameters that a System.Action or System.Func delegate can have.
    /// </summary>
    private const int MaxSystemDelegateParameters = 16;

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        GetActionFuncDelegates(appContext, out var actionTypes, out var funcTypes);

        var multicastDelegateType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.MulticastDelegate");
        var asyncCallbackType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.AsyncCallback");
        var iasyncResultType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.IAsyncResult");

        var delegateSupportType = appContext.ResolveTypeOrThrow(typeof(DelegateSupport));
        var delegateSupportMethod = delegateSupportType.GetMethodByName(nameof(DelegateSupport.ConvertDelegate));

        var il2CppSystemDelegate = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Delegate");
        var il2CppSystemDelegateCombine = il2CppSystemDelegate.Methods.Single(m => m.Name == "Combine" && m.Parameters.Count == 2);
        var il2CppSystemDelegateRemove = il2CppSystemDelegate.GetMethodByName("Remove");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            // for rather than foreach, as we will be adding items to the collection
            for (var typeIndex = 0; typeIndex < assembly.Types.Count; typeIndex++)
            {
                var type = assembly.Types[typeIndex];
                if (!type.IsIl2CppDelegate)
                    continue;

                // Remove variance on generic parameters because only interfaces and (real) delegates can have variance.
                // https://github.com/dotnet/csharplang/discussions/2498
                // https://learn.microsoft.com/en-us/dotnet/standard/generics/covariance-and-contravariance
                {
                    // Before: Func<in T1, in T2, out TResult>
                    // After: Func<T1, T2, TResult>
                    //
                    // This inherently makes some code invalid, both normal and unstripped.
                    // There's probably no (good) way to fix that because the runtime doesn't actually change the type.
                    // https://lab.razor.fyi/#hZDBSsRADIaRetCcxCfIcXrpA7giqIsFURFcEDw5zoYamM6UmbSwLD36BB68-wC-nyels3VVFM0hhPzk-38CzxnAZfBV0HVh4u5j1kZ2FV4tolA9AWjaO8sGjdUx4pGOhEvov2-nFLijOe596ith5MISEBGjaGGDnec5nmt2Kk_rlTjUsXfRWyquAwudsSNVkoxwlRclyWzRkMqLk9baC11TPkm3fepN4E4LfdgcGmHv9odEB1iSDMMPx0DSBodTP7tnV_1PG8Mk4DrY78y15R_U9IrRXKXfiQ4VyVdkDz2cZqF1N9tbLw9vT6-0s3m78Q4
                    // One way the problem might be solved is to:
                    // * Generate a nested interface for every Il2Cpp delegate type, eg IFunc<in T1, in T2, out TResult>.
                    // * Replace all occurances of the class type with the interface type, everywhere.
                    // * During unstripping, redirect references to the class methods to the interface methods.

                    foreach (var genericParameter in type.GenericParameters)
                    {
                        genericParameter.OverrideAttributes = genericParameter.Attributes & ~GenericParameterAttributes.VarianceMask;
                    }
                }

                var concreteType = type.SelfInstantiateIfGeneric();

                var addition = new InjectedMethodAnalysisContext(
                    type,
                    "op_Addition",
                    concreteType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [concreteType, concreteType])
                {
                    IsInjected = true,
                };
                type.Methods.Add(addition);
                addition.PutExtraData<NativeMethodBody>(new()
                {
                    Instructions =
                    [
                        new Instruction(CilOpCodes.Ldarg_0),
                        new Instruction(CilOpCodes.Ldarg_1),
                        new Instruction(CilOpCodes.Call, il2CppSystemDelegateCombine),
                        new Instruction(CilOpCodes.Castclass, concreteType),
                        new Instruction(CilOpCodes.Ret),
                    ]
                });

                var subtraction = new InjectedMethodAnalysisContext(
                    type,
                    "op_Subtraction",
                    concreteType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [concreteType, concreteType])
                {
                    IsInjected = true,
                };
                type.Methods.Add(subtraction);
                subtraction.PutExtraData<NativeMethodBody>(new()
                {
                    Instructions =
                    [
                        new Instruction(CilOpCodes.Ldarg_0),
                        new Instruction(CilOpCodes.Ldarg_1),
                        new Instruction(CilOpCodes.Call, il2CppSystemDelegateRemove),
                        new Instruction(CilOpCodes.Castclass, concreteType),
                        new Instruction(CilOpCodes.Ret),
                    ]
                });

                var invokeMethod = type.TryGetMethodByName("Invoke");
                if (invokeMethod is null)
                {
                    // The delegate does not have an Invoke method.
                    // This can happen if the delegate is unstripped, but a parameter type could not unstripped.
                    continue;
                }

                Debug.Assert(invokeMethod.ReturnType is not PointerTypeAnalysisContext and not ByRefTypeAnalysisContext, "All pointers and by reference types should be converted to generics");
                Debug.Assert(invokeMethod.Parameters.All(p => p.ParameterType is not PointerTypeAnalysisContext and not ByRefTypeAnalysisContext), "All pointers and by reference types should be converted to generics");

                TypeAnalysisContext managedDelegateType;

                if (invokeMethod.Parameters.Count <= MaxSystemDelegateParameters)
                {
                    // We can use a System delegate

                    if (!invokeMethod.IsVoid)
                    {
                        managedDelegateType = funcTypes[invokeMethod.Parameters.Count]
                            .MakeGenericInstanceType(invokeMethod.Parameters.Select(p => p.ParameterType).Append(invokeMethod.ReturnType));
                    }
                    else if (invokeMethod.Parameters.Count > 0)
                    {
                        managedDelegateType = actionTypes[invokeMethod.Parameters.Count]
                            .MakeGenericInstanceType(invokeMethod.Parameters.Select(p => p.ParameterType));
                    }
                    else
                    {
                        managedDelegateType = actionTypes[0];
                    }
                }
                else
                {
                    // We need to create a new delegate type

                    var name = type.Name is "Delegate" ? "Converted" : "Delegate"; // Name can't be the same as the declaring type
                    var injectedType = type.InjectNestedType(
                        name,
                        multicastDelegateType);

                    injectedType.IsInjected = true;
                    injectedType.CopyGenericParameters(type, true);

                    TypeAnalysisContext returnType;
                    List<TypeAnalysisContext> parameterTypes = invokeMethod.Parameters.Select(p => p.ParameterType).ToList();
                    {
                        var genericParameterDictionary = Enumerable.Range(0, type.GenericParameters.Count)
                            .ToDictionary<int, TypeAnalysisContext, TypeAnalysisContext>(i => type.GenericParameters[i], i => injectedType.GenericParameters[i]);
                        var replacementVisitor = new TypeReplacementVisitor(genericParameterDictionary);
                        replacementVisitor.Modify(parameterTypes);
                        returnType = replacementVisitor.Replace(invokeMethod.ReturnType);
                    }

                    // Constructor
                    {
                        injectedType.Methods.Add(new InjectedMethodAnalysisContext(
                            injectedType,
                            ".ctor",
                            appContext.SystemTypes.SystemVoidType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                            [appContext.SystemTypes.SystemObjectType, appContext.SystemTypes.SystemIntPtrType],
                            defaultImplAttributes: MethodImplAttributes.Runtime));
                    }

                    // Invoke
                    {
                        injectedType.Methods.Add(new InjectedMethodAnalysisContext(
                            injectedType,
                            "Invoke",
                            returnType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                            parameterTypes.ToArray(),
                            defaultImplAttributes: MethodImplAttributes.Runtime));
                    }

                    // BeginInvoke
                    {
                        injectedType.Methods.Add(new InjectedMethodAnalysisContext(
                            injectedType,
                            "BeginInvoke",
                            iasyncResultType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                            parameterTypes.Append(asyncCallbackType).Append(appContext.SystemTypes.SystemObjectType).ToArray(),
                            defaultImplAttributes: MethodImplAttributes.Runtime));
                    }

                    // EndInvoke
                    {
                        injectedType.Methods.Add(new InjectedMethodAnalysisContext(
                            injectedType,
                            "EndInvoke",
                            returnType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                            [iasyncResultType],
                            defaultImplAttributes: MethodImplAttributes.Runtime));
                    }

                    if (injectedType.GenericParameters.Count > 0)
                    {
                        managedDelegateType = injectedType.MakeGenericInstanceType(type.GenericParameters);
                    }
                    else
                    {
                        managedDelegateType = injectedType;
                    }
                }

                type.ManagedDelegateType = managedDelegateType;

                // Explicit conversion operator from the managed delegate type to the Il2Cpp delegate type.
                {
                    var explicitConversion = new InjectedMethodAnalysisContext(
                        type,
                        "op_Explicit",
                        concreteType,
                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                        [managedDelegateType])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(explicitConversion);
                    explicitConversion.PutExtraData<NativeMethodBody>(new()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, delegateSupportMethod.MakeGenericInstanceMethod(concreteType)),
                            new Instruction(CilOpCodes.Ret),
                        ]
                    });
                }

                // Explicit conversion operator from the Il2Cpp delegate type to the managed delegate type.
                {
                    var explicitConversion = new InjectedMethodAnalysisContext(
                        type,
                        "op_Explicit",
                        managedDelegateType,
                        MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                        [concreteType])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(explicitConversion);

                    MethodAnalysisContext managedDelegateConstructor;
                    if (managedDelegateType is GenericInstanceTypeAnalysisContext genericInstance)
                    {
                        managedDelegateConstructor = genericInstance.GenericType.Methods.Single(m => m.IsInstanceConstructor).MakeConcreteGenericMethod(genericInstance.GenericArguments, []);
                    }
                    else
                    {
                        managedDelegateConstructor = managedDelegateType.Methods.Single(m => m.IsInstanceConstructor);
                    }

                    explicitConversion.PutExtraData<NativeMethodBody>(new()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Ldftn, invokeMethod.MaybeMakeConcreteGeneric(type.GenericParameters, [])),
                            new Instruction(CilOpCodes.Newobj, managedDelegateConstructor),
                            new Instruction(CilOpCodes.Ret),
                        ]
                    });
                }
            }
        }
    }

    private static void GetActionFuncDelegates(ApplicationAnalysisContext appContext, out TypeAnalysisContext[] actionTypes, out TypeAnalysisContext[] funcTypes)
    {
        var mscorlib = appContext.Mscorlib;

        actionTypes = Enumerable.Range(0, MaxSystemDelegateParameters + 1)
            .Select(i => GetActionType(mscorlib, i))
            .ToArray();

        funcTypes = Enumerable.Range(0, MaxSystemDelegateParameters + 1)
            .Select(i => GetFuncType(mscorlib, i))
            .ToArray();

        static TypeAnalysisContext GetActionType(AssemblyAnalysisContext mscorlib, int parameterCount) => parameterCount switch
        {
            0 => mscorlib.GetTypeByFullNameOrThrow("System.Action"),
            _ => mscorlib.GetTypeByFullNameOrThrow($"System.Action`{parameterCount}"),
        };
        static TypeAnalysisContext GetFuncType(AssemblyAnalysisContext mscorlib, int parameterCount) => mscorlib.GetTypeByFullNameOrThrow($"System.Func`{parameterCount + 1}");
    }
}
