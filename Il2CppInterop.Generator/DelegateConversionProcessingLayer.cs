using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator;

public class DelegateConversionProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Delegate Conversion";

    public override string Id => "delegate_conversion";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        PolyfillActionFuncDelegates(appContext);

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
                if (type.BaseType is not { Namespace: "Il2CppSystem", Name: "MulticastDelegate" })
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

                var invokeMethod = type.GetMethodByName("Invoke");

                TypeAnalysisContext managedDelegateType;

                if (invokeMethod.Parameters.Count <= 16
                    && invokeMethod.ReturnType is not PointerTypeAnalysisContext and not ByRefTypeAnalysisContext
                    && invokeMethod.Parameters.All(p => p.ParameterType is not PointerTypeAnalysisContext and not ByRefTypeAnalysisContext))
                {
                    // We can use a System delegate

                    if (!invokeMethod.IsVoid)
                    {
                        var systemType = invokeMethod.Parameters.Count switch
                        {
                            0 => typeof(Func<>),
                            1 => typeof(Func<,>),
                            2 => typeof(Func<,,>),
                            3 => typeof(Func<,,,>),
                            4 => typeof(Func<,,,,>),
                            5 => typeof(Func<,,,,,>),
                            6 => typeof(Func<,,,,,,>),
                            7 => typeof(Func<,,,,,,,>),
                            8 => typeof(Func<,,,,,,,,>),
                            9 => typeof(Func<,,,,,,,,,>),
                            10 => typeof(Func<,,,,,,,,,,>),
                            11 => typeof(Func<,,,,,,,,,,,>),
                            12 => typeof(Func<,,,,,,,,,,,,>),
                            13 => typeof(Func<,,,,,,,,,,,,,>),
                            14 => typeof(Func<,,,,,,,,,,,,,,>),
                            15 => typeof(Func<,,,,,,,,,,,,,,,>),
                            16 => typeof(Func<,,,,,,,,,,,,,,,,>),
                            _ => default!, // unreachable
                        };
                        managedDelegateType = appContext.ResolveTypeOrThrow(systemType)
                            .MakeGenericInstanceType(invokeMethod.Parameters.Select(p => p.ParameterType).Append(invokeMethod.ReturnType));
                    }
                    else if (invokeMethod.Parameters.Count > 0)
                    {
                        var systemType = invokeMethod.Parameters.Count switch
                        {
                            1 => typeof(Action<>),
                            2 => typeof(Action<,>),
                            3 => typeof(Action<,,>),
                            4 => typeof(Action<,,,>),
                            5 => typeof(Action<,,,,>),
                            6 => typeof(Action<,,,,,>),
                            7 => typeof(Action<,,,,,,>),
                            8 => typeof(Action<,,,,,,,>),
                            9 => typeof(Action<,,,,,,,,>),
                            10 => typeof(Action<,,,,,,,,,>),
                            11 => typeof(Action<,,,,,,,,,,>),
                            12 => typeof(Action<,,,,,,,,,,,>),
                            13 => typeof(Action<,,,,,,,,,,,,>),
                            14 => typeof(Action<,,,,,,,,,,,,,>),
                            15 => typeof(Action<,,,,,,,,,,,,,,>),
                            16 => typeof(Action<,,,,,,,,,,,,,,,>),
                            _ => default!, // unreachable
                        };
                        managedDelegateType = appContext.ResolveTypeOrThrow(systemType).MakeGenericInstanceType(invokeMethod.Parameters.Select(p => p.ParameterType));
                    }
                    else
                    {
                        managedDelegateType = appContext.ResolveTypeOrThrow(typeof(Action));
                    }
                }
                else
                {
                    // We need to create a new delegate type

                    var name = type.Name is "Delegate" ? "Converted" : "Delegate"; // Name can't be the same as the declaring type
                    managedDelegateType = type.InjectNestedType(
                        name,
                        multicastDelegateType);

                    managedDelegateType.CopyGenericParameters(type);

                    TypeAnalysisContext returnType;
                    List<TypeAnalysisContext> parameterTypes = invokeMethod.Parameters.Select(p => p.ParameterType).ToList();
                    {
                        var genericParameterDictionary = Enumerable.Range(0, type.GenericParameters.Count)
                            .ToDictionary<int, TypeAnalysisContext, TypeAnalysisContext>(i => type.GenericParameters[i], i => managedDelegateType.GenericParameters[i]);
                        var replacementVisitor = new TypeReplacementVisitor(genericParameterDictionary);
                        replacementVisitor.Replace(parameterTypes);
                        returnType = replacementVisitor.Replace(invokeMethod.ReturnType);
                    }

                    // Constructor
                    {
                        managedDelegateType.Methods.Add(new InjectedMethodAnalysisContext(
                            managedDelegateType,
                            ".ctor",
                            appContext.SystemTypes.SystemVoidType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                            [appContext.SystemTypes.SystemObjectType, appContext.SystemTypes.SystemIntPtrType],
                            defaultImplAttributes: MethodImplAttributes.Runtime)
                        {
                            RuntimeImplemented = true,
                        });
                    }

                    // Invoke
                    {
                        managedDelegateType.Methods.Add(new InjectedMethodAnalysisContext(
                            managedDelegateType,
                            "Invoke",
                            returnType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                            parameterTypes.ToArray(),
                            defaultImplAttributes: MethodImplAttributes.Runtime)
                        {
                            RuntimeImplemented = true,
                        });
                    }

                    // BeginInvoke
                    {
                        managedDelegateType.Methods.Add(new InjectedMethodAnalysisContext(
                            managedDelegateType,
                            "BeginInvoke",
                            iasyncResultType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                            parameterTypes.Append(asyncCallbackType).Append(appContext.SystemTypes.SystemObjectType).ToArray(),
                            defaultImplAttributes: MethodImplAttributes.Runtime)
                        {
                            RuntimeImplemented = true,
                        });
                    }

                    // EndInvoke
                    {
                        managedDelegateType.Methods.Add(new InjectedMethodAnalysisContext(
                            managedDelegateType,
                            "EndInvoke",
                            returnType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                            [iasyncResultType],
                            defaultImplAttributes: MethodImplAttributes.Runtime)
                        {
                            RuntimeImplemented = true,
                        });
                    }
                }

                var concreteType = type.HasGenericParameters ? type.MakeGenericInstanceType(type.GenericParameters) : type;
                var explicitConversion = new InjectedMethodAnalysisContext(
                    type,
                    "op_Explicit",
                    concreteType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [managedDelegateType]);
                type.Methods.Add(explicitConversion);
                explicitConversion.PutExtraData<TranslatedMethodBody>(new()
                {
                    Instructions =
                    [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Call, delegateSupportMethod.MakeGenericInstanceMethod(concreteType)),
                        new Instruction(OpCodes.Ret),
                    ]
                });

                var addition = new InjectedMethodAnalysisContext(
                    type,
                    "op_Addition",
                    concreteType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [concreteType, concreteType]);
                type.Methods.Add(addition);
                addition.PutExtraData<TranslatedMethodBody>(new()
                {
                    Instructions =
                    [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Ldarg_1),
                        new Instruction(OpCodes.Call, il2CppSystemDelegateCombine),
                        new Instruction(OpCodes.Castclass, concreteType),
                        new Instruction(OpCodes.Ret),
                    ]
                });

                var subtraction = new InjectedMethodAnalysisContext(
                    type,
                    "op_Subtraction",
                    concreteType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [concreteType, concreteType]);
                type.Methods.Add(subtraction);
                subtraction.PutExtraData<TranslatedMethodBody>(new()
                {
                    Instructions =
                    [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Ldarg_1),
                        new Instruction(OpCodes.Call, il2CppSystemDelegateRemove),
                        new Instruction(OpCodes.Castclass, concreteType),
                        new Instruction(OpCodes.Ret),
                    ]
                });
            }
        }
    }

    /// <summary>
    /// mscorlib only contains Action and Func delegates with up to 8 parameters.
    /// However, System.Private.CoreLib contains Action and Func delegates with up to 16 parameters.
    /// This method will create the Action and Func delegates with more than 8 parameters,
    /// so that they can be used when the mscorlib assembly references are replaced with System.Private.CoreLib references.
    /// </summary>
    /// <param name="appContext"></param>
    private static void PolyfillActionFuncDelegates(ApplicationAnalysisContext appContext)
    {
        var mscorlib = appContext.AssembliesByName["mscorlib"];

        ReadOnlySpan<Type> types =
        [
            typeof(Action),
            typeof(Action<>),
            typeof(Action<,>),
            typeof(Action<,,>),
            typeof(Action<,,,>),
            typeof(Action<,,,,>),
            typeof(Action<,,,,,>),
            typeof(Action<,,,,,,>),
            typeof(Action<,,,,,,,>),
            typeof(Action<,,,,,,,,>),
            typeof(Action<,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,,>),
            typeof(Action<,,,,,,,,,,,,,,,>),
            typeof(Func<>),
            typeof(Func<,>),
            typeof(Func<,,>),
            typeof(Func<,,,>),
            typeof(Func<,,,,>),
            typeof(Func<,,,,,>),
            typeof(Func<,,,,,,>),
            typeof(Func<,,,,,,,>),
            typeof(Func<,,,,,,,,>),
            typeof(Func<,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,,>),
            typeof(Func<,,,,,,,,,,,,,,,,>),
        ];

        foreach (var type in types)
        {
            if (mscorlib.GetTypeByFullName(type.FullName!) is not null)
                continue;

            mscorlib.InjectType(type).InjectContentFromSourceType();
        }
    }
}
