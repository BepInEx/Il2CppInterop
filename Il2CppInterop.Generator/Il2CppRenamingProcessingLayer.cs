using System.Runtime.CompilerServices;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class Il2CppRenamingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Il2Cpp Name Changes";

    public override string Id => "il2cpprenamer";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        Logger.InfoNewline("Renaming assemblies and types to Il2Cpp", nameof(Il2CppRenamingProcessingLayer));

        var assemblyCount = appContext.Assemblies.Count;
        for (var i = 0; i < assemblyCount; i++)
        {
            var assembly = appContext.Assemblies[i];

            if (!IsUnity(assembly.Name) && !IsAssemblyCSharp(assembly.Name))
            {
                assembly.OverrideName = "Il2Cpp" + assembly.Name;
            }

            foreach (var type in assembly.Types)
            {
                if (!IsUnity(type.Namespace))
                {
                    type.OverrideNamespace = "Il2Cpp" + type.Namespace;
                }
            }

            ResetTypesByName(assembly);

            progressCallback?.Invoke(i, assemblyCount);
        }

        ResetAssembliesByName(appContext);
    }

    private static bool IsUnity(string name)
    {
        return name.StartsWith("Unity", StringComparison.Ordinal);
    }

    private static bool IsAssemblyCSharp(string name)
    {
        return name.StartsWith("Assembly-CSharp", StringComparison.Ordinal);
    }

    private static void ResetAssembliesByName(ApplicationAnalysisContext appContext)
    {
        var dictionary = appContext.AssembliesByName;
        dictionary.Clear();
        foreach (var assembly in appContext.Assemblies)
        {
            dictionary[assembly.Name] = assembly;
        }
    }

    private static void ResetTypesByName(AssemblyAnalysisContext assembly)
    {
        var dictionary = GetTypesByName(assembly);
        dictionary.Clear();
        foreach (var type in assembly.Types)
        {
            dictionary[type.FullName] = type;
        }
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "TypesByName")]
    private static extern ref Dictionary<string, TypeAnalysisContext> GetTypesByName(AssemblyAnalysisContext assembly);
}
