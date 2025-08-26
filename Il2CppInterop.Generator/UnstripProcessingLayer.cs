using AsmResolver.DotNet;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class UnstripProcessingLayer : UnstripBaseProcessingLayer
{
    public override string Name => "Unstrip external assemblies into the Cpp2IL context system";

    public override string Id => "unstrip";

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
                .Select(AssemblyDefinition.FromFile)
                .ToList();
        }

        if (assemblyList.Count == 0)
        {
            Logger.WarnNewline("No assemblies provided - processor will not run.", nameof(UnstripProcessingLayer));
            return;
        }

        Logger.InfoNewline($"Unstripping {assemblyList.Count} assemblies...", nameof(UnstripProcessingLayer));

        InjectAssemblies(appContext, assemblyList, true);
    }
}
