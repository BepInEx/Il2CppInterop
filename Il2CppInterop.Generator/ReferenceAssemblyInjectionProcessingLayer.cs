using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

public class ReferenceAssemblyInjectionProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "reference_assembly_injector";
    public override string Name => "Inject required references into the Cpp2IL context system";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        Type[] il2CppInteropCommonTypes =
        [
            typeof(ObjectPointer),
        ];
        InjectTypes(appContext, typeof(ObjectPointer).Assembly, il2CppInteropCommonTypes);

        Type[] il2CppInteropRuntimeTypes =
        [
            typeof(Il2CppArrayBase),
            typeof(Il2CppArrayBase<>),
            typeof(Il2CppUnmanagedArray<>),
            typeof(Il2CppStringArray),
            typeof(Il2CppException),
            typeof(IL2CPP),
            typeof(Il2CppClassPointerStore<>),
            typeof(DelegateSupport),
            typeof(Pointer<>),
            typeof(ByReference<>),
            typeof(Il2CppType),
            typeof(FieldAccessHelper),
            typeof(IIl2CppObjectBase),
            typeof(IIl2CppType),
            typeof(IIl2CppType<>),
            typeof(IIl2CppException),
            typeof(RuntimeInvokeHelper),
            typeof(Il2CppTypeHelper),
        ];
        InjectTypes(appContext, typeof(Il2CppArrayBase).Assembly, il2CppInteropRuntimeTypes);
    }

    /// <summary>
    /// Injects the given assembly and some of its types into the <see cref="ApplicationAnalysisContext"/>.
    /// </summary>
    /// <param name="appContext">The <see cref="ApplicationAnalysisContext"/></param>
    /// <param name="assembly">The assembly</param>
    /// <param name="types">The types to be injected from <paramref name="assembly"/>. Must be in order of inheritance</param>
    private static void InjectTypes(ApplicationAnalysisContext appContext, Assembly assembly, Type[] types)
    {
        var il2CppInteropRuntime = appContext.InjectAssembly(assembly);

        il2CppInteropRuntime.IsReferenceAssembly = true;

        var typeContextArray = new InjectedTypeAnalysisContext[types.Length];

        for (var i = 0; i < types.Length; i++)
        {
            typeContextArray[i] = il2CppInteropRuntime.InjectType(types[i]);
        }

        for (var index = 0; index < types.Length; index++)
        {
            typeContextArray[index].InjectContentFromSourceType();
        }
    }
}
