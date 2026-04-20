using System.Diagnostics;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

/// <summary>
/// 3 virtual methods in Il2CppSystem.Object conflict with their System.Object counterparts.
/// </summary>
public partial class ConflictRenamingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Conflict Renaming";
    public override string Id => "conflictrenamer";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // ToString => ToIl2CppString
        // GetHashCode => GetIl2CppHashCode
        // Finalize => Il2CppFinalize

        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");

        for (var i = 0; i < appContext.Assemblies.Count; i++)
        {
            var assembly = appContext.Assemblies[i];

            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                MaybeAppendUnderscore(type.Name, type);

                foreach (var field in type.Fields)
                {
                    MaybeAppendUnderscore(field.Name, field);
                }

                foreach (var property in type.Properties)
                {
                    MaybeAppendUnderscore(property.Name, property);
                }

                foreach (var @event in type.Events)
                {
                    MaybeAppendUnderscore(@event.Name, @event);
                }

                foreach (var method in type.Methods)
                {
                    var name = method.Name;
                    switch (name)
                    {
                        case "ToString":
                            if (method.Parameters.Count == 0 && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                method.Name = "ToIl2CppString";
                            }
                            break;
                        case "GetHashCode":
                            if (method.Parameters.Count == 0 && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                method.Name = "GetIl2CppHashCode";
                            }
                            break;
                        case "Finalize":
                            if (method.Parameters.Count == 0 && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                method.Name = "Il2CppFinalize";
                                method.Overrides.Clear(); // Since this is no longer the Finalize method, it shouldn't have an explicit override.
                            }
                            break;
                        default:
                            MaybeAppendUnderscore(name, method);
                            break;
                    }
                }
            }

            progressCallback?.Invoke(i, appContext.Assemblies.Count);
        }
    }

    private static void MaybeAppendUnderscore(string name, HasCustomAttributesAndName context)
    {
        // If the name matches any of the patterns, append an underscore to avoid conflicts.
        if (ToStringRegex.IsMatch(name))
        {
            context.Name = $"{name}_";
        }
        else if (GetHashCodeRegex.IsMatch(name))
        {
            context.Name = $"{name}_";
        }
        else if (FinalizeRegex.IsMatch(name))
        {
            context.Name = $"{name}_";
        }
    }

    [GeneratedRegex(@"^ToIl2CppString_*$")]
    private static partial Regex ToStringRegex { get; }

    [GeneratedRegex(@"^GetIl2CppHashCode_*$")]
    private static partial Regex GetHashCodeRegex { get; }

    [GeneratedRegex(@"^Il2CppFinalize_*$")]
    private static partial Regex FinalizeRegex { get; }
}
