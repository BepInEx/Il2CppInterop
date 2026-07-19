using System.Diagnostics;
using AsmResolver.DotNet;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class MscorlibAssemblyInjectionProcessingLayer : UnstripBaseProcessingLayer
{
    public override string Name => "Inject a new mscorlib into the Cpp2IL context system";

    public override string Id => "mscorlib_injector";

    private static readonly string[] injectedAssemblies =
    [
        "mscorlib",
        "System.Collections",
    ];
    internal static ReadOnlySpan<string> InjectedAssemblies => injectedAssemblies;

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        Logger.InfoNewline($"Injecting new mscorlib...", nameof(MscorlibAssemblyInjectionProcessingLayer));

        var mscorlib = LoadSystemRuntime();
        var runtimeContext = new RuntimeContext(DotNetRuntimeInfo.NetCoreApp(10, 0), new AssemblyResolver(), mscorlib);
        runtimeContext.AddAssembly(mscorlib);

        var assembliesToInject = new AssemblyDefinition[injectedAssemblies.Length];
        assembliesToInject[0] = mscorlib;
        for (var i = 1; i < injectedAssemblies.Length; i++)
        {
            var assemblyName = injectedAssemblies[i];
            assembliesToInject[i] = LoadAssembly(assemblyName, runtimeContext);
        }

        InjectAssemblies(appContext, assembliesToInject, false, true);

        // Need to reset the system types context to use the new corlib
        appContext.SystemTypes = new SystemTypesContext(appContext);

        foreach (var assemblyName in injectedAssemblies)
        {
            appContext.AssembliesByName[assemblyName].IsReferenceAssembly = true;
        }
    }

    private static AssemblyDefinition LoadSystemRuntime()
    {
        AssemblyDefinition assembly = AssemblyDefinition.FromBytes(Basic.Reference.Assemblies.Net100.ReferenceInfos.SystemRuntime.ImageBytes, createRuntimeContext: false);
        assembly.Name = "mscorlib";
        assembly.ManifestModule!.Name = "mscorlib.dll";
        return assembly;
    }

    private static AssemblyDefinition LoadAssembly(string assemblyName, RuntimeContext runtimeContext)
    {
        var assembly = AssemblyDefinition.FromBytes(GetAssemblyBytes($"{assemblyName}.dll"), createRuntimeContext: false);
        foreach (var module in assembly.Modules)
        {
            foreach (var assemblyReference in module.AssemblyReferences)
            {
                if (assemblyReference.Name == "System.Runtime")
                {
                    assemblyReference.Name = "mscorlib";
                }
            }
        }
        runtimeContext.AddAssembly(assembly);
        return assembly;
    }

    private static byte[] GetAssemblyBytes(string fileName)
    {
        return Basic.Reference.Assemblies.Net100.ReferenceInfos.All.First(x => x.FileName == fileName).ImageBytes;
    }

    private sealed class AssemblyResolver : IAssemblyResolver
    {
        public ResolutionStatus Resolve(AssemblyDescriptor assembly, ModuleDefinition? originModule, out AssemblyDefinition? result)
        {
            Debug.Fail("Resolution of external assemblies should not be necessary.");
            result = null;
            return ResolutionStatus.AssemblyNotFound;
        }
    }
}
