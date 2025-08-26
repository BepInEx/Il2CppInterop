using AsmResolver.DotNet;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class MscorlibAssemblyInjectionProcessingLayer : UnstripBaseProcessingLayer
{
    public override string Name => "Inject a new mscorlib into the Cpp2IL context system";

    public override string Id => "mscorlib_injector";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var assemblyList = appContext.GetExtraData<IReadOnlyList<AssemblyDefinition>>(AssembliesKey);
        if (assemblyList is null)
        {
            var directoryPath = appContext.GetExtraData<string>(DirectoryKey);
            if (directoryPath is null)
            {
                Logger.WarnNewline($"No assemblies specified - processor will not run. You need to provide the {DirectoryKey}, either by programmatically adding it as extra data in the app context, or by specifying it in the --processor-config command line option.", nameof(UnstripProcessingLayer));
                return;
            }

            assemblyList = Directory.GetFiles(directoryPath, "*.dll", SearchOption.AllDirectories)
                .Where(x => x.Contains("mscorlib", StringComparison.OrdinalIgnoreCase))
                .Select(AssemblyDefinition.FromFile)
                .ToList();
        }

        var mscorlib = assemblyList.FirstOrDefault(x => x.Name == "mscorlib");

        if (mscorlib is null)
        {
            Logger.WarnNewline("mscorlib not provided - processor will not run.", nameof(UnstripProcessingLayer));
            return;
        }

        if (appContext.AssembliesByName.ContainsKey("mscorlib"))
        {
            Logger.WarnNewline("mscorlib already injected - processor will not run.", nameof(MscorlibAssemblyInjectionProcessingLayer));
            return;
        }

        Logger.InfoNewline($"Injecting new mscorlib...", nameof(MscorlibAssemblyInjectionProcessingLayer));

        InjectAssemblies(appContext, [mscorlib], false);

        // Need to reset the system types context to use the new corlib
        appContext.SystemTypes = new SystemTypesContext(appContext);

        appContext.AssembliesByName["mscorlib"].IsReferenceAssembly = true;
    }
}
