using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass89GenerateForwarders
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var targetAssembly = context.TryGetAssemblyByName("UnityEngine");
        if (targetAssembly == null)
        {
            Logger.Instance.LogInformation("No UnityEngine.dll, will not generate forwarders");
            return;
        }

        var targetModule = targetAssembly.NewAssembly.ManifestModule;

        foreach (var assemblyRewriteContext in context.Assemblies)
        {
            if (!assemblyRewriteContext.NewAssembly.Name.StartsWith("UnityEngine.")) continue;
            foreach (var mainModuleType in assemblyRewriteContext.NewAssembly.ManifestModule!.TopLevelTypes)
            {
                if (mainModuleType.Name == "<Module>")
                    continue;

                var exportedType = new ExportedType(null, mainModuleType.Namespace, mainModuleType.Name)
                {
                    Attributes = TypeAttributes.Forwarder
                };
                targetModule!.ExportedTypes.Add(exportedType);

                AddNestedTypes(mainModuleType, exportedType, targetModule);
            }
        }
    }

    private static void AddNestedTypes(TypeDefinition mainModuleType, ExportedType importedType,
        ModuleDefinition targetModule)
    {
        foreach (var nested in mainModuleType.NestedTypes)
        {
            if ((nested.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.NestedPublic) continue;

            var nestedImport = targetModule.DefaultImporter.ImportType(nested);
            var nestedExport = new ExportedType(importedType, nestedImport.Namespace, nestedImport.Name)
            {
                Attributes = TypeAttributes.Forwarder
            };

            targetModule.ExportedTypes.Add(nestedExport);

            AddNestedTypes(nested, nestedExport, targetModule);
        }
    }
}
