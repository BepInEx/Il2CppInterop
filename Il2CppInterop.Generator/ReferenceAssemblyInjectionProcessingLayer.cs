using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Exceptions;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

public class ReferenceAssemblyInjectionProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "reference_assembly_injector";
    public override string Name => "Inject required references into the Cpp2IL context system";

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code", Justification = "The accessed members are never invoked by this processing layer, only viewed for reference.")]
    [SuppressMessage("Trimming", "IL2111:Method with parameters or return value with `DynamicallyAccessedMembersAttribute` is accessed via reflection. Trimmer can't guarantee availability of the requirements of the method.", Justification = "The accessed members are never invoked by this processing layer, only viewed for reference.")]
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // Types need to be provided twice, so that the linker can find them

        ReadOnlySpan<Type> il2CppInteropCommonTypes =
        [
            typeof(IL2CPP),
            typeof(ObjectPointer),
            typeof(Il2CppType),
            typeof(Il2CppObjectPool),

            typeof(Il2CppMemberAttribute),
            typeof(Il2CppMethodAttribute),
            typeof(Il2CppFieldAttribute),
            typeof(Il2CppPropertyAttribute),
            typeof(Il2CppEventAttribute),
            typeof(Il2CppTypeAttribute),
            typeof(Il2CppAssemblyAttribute),

            typeof(IIl2CppType),
            typeof(IIl2CppType<>),
        ];
        {
            var injectedAssembly = CreateTypes(appContext, typeof(IL2CPP).Assembly, il2CppInteropCommonTypes);

            InjectContentFromSourceType(injectedAssembly, typeof(IL2CPP));
            InjectContentFromSourceType(injectedAssembly, typeof(ObjectPointer));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppType));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppObjectPool));

            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppMemberAttribute));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppMethodAttribute));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppFieldAttribute));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppPropertyAttribute));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppEventAttribute));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppTypeAttribute));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppAssemblyAttribute));

            InjectContentFromSourceType(injectedAssembly, typeof(IIl2CppType));
            InjectContentFromSourceType(injectedAssembly, typeof(IIl2CppType<>));
        }

        ReadOnlySpan<Type> il2CppInteropRuntimeTypes =
        [
            typeof(Il2CppArrayBase),
            typeof(Il2CppArrayBase<>),
            typeof(Il2CppArrayRank1<>),
            typeof(Il2CppArrayRank2<>),
            typeof(Il2CppArrayRank3<>),
            typeof(Il2CppArrayRank4<>),
            typeof(Il2CppArrayRank5<>),

            typeof(Il2CppException),
            typeof(TypeInjector),
            typeof(DelegateSupport),
            typeof(RuntimeInvoke),
            typeof(FieldAccess),
            typeof(Pointer<>),
            typeof(ByReference<>),
            typeof(ByReference),
            typeof(IIl2CppException),
            typeof(NativeBoxing),
            typeof(GenerationInternals),
        ];
        {
            var injectedAssembly = CreateTypes(appContext, typeof(Il2CppArrayBase).Assembly, il2CppInteropRuntimeTypes);

            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayBase));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayBase<>));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayRank1<>));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayRank2<>));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayRank3<>));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayRank4<>));
            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppArrayRank5<>));

            InjectContentFromSourceType(injectedAssembly, typeof(Il2CppException));
            InjectContentFromSourceType(injectedAssembly, typeof(TypeInjector));
            InjectContentFromSourceType(injectedAssembly, typeof(DelegateSupport));
            InjectContentFromSourceType(injectedAssembly, typeof(RuntimeInvoke));
            InjectContentFromSourceType(injectedAssembly, typeof(FieldAccess));
            InjectContentFromSourceType(injectedAssembly, typeof(Pointer<>));
            InjectContentFromSourceType(injectedAssembly, typeof(ByReference<>));
            InjectContentFromSourceType(injectedAssembly, typeof(ByReference));
            InjectContentFromSourceType(injectedAssembly, typeof(IIl2CppException));
            InjectContentFromSourceType(injectedAssembly, typeof(NativeBoxing));
            InjectContentFromSourceType(injectedAssembly, typeof(GenerationInternals));
        }
    }

    /// <summary>
    /// Injects the given assembly and some of its types into the <see cref="ApplicationAnalysisContext"/>.
    /// </summary>
    /// <param name="appContext">The <see cref="ApplicationAnalysisContext"/></param>
    /// <param name="assembly">The assembly</param>
    /// <param name="types">The types to be injected from <paramref name="assembly"/>. Must be in order of inheritance</param>
    private static InjectedAssemblyAnalysisContext CreateTypes(ApplicationAnalysisContext appContext, Assembly assembly, ReadOnlySpan<Type> types)
    {
        var injectedAssembly = appContext.InjectAssembly(assembly);

        injectedAssembly.IsReferenceAssembly = true;

        var typeContextArray = new InjectedTypeAnalysisContext[types.Length];

        for (var i = 0; i < types.Length; i++)
        {
            typeContextArray[i] = injectedAssembly.InjectType(types[i]);
        }

        return injectedAssembly;
    }

    private static void InjectContentFromSourceType(AssemblyAnalysisContext assembly, [DynamicallyAccessedMembers(InjectedTypeAnalysisContextExtensions.AccessedMemberTypes)] Type sourceType)
    {
        var type = (InjectedTypeAnalysisContext)assembly.GetTypeByFullNameOrThrow(sourceType);
        type.InjectContentFromSourceType(sourceType);
    }
}
