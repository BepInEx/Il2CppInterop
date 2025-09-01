using System.Diagnostics;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

/// <summary>
/// 4 virtual methods in Il2CppSystem.Object conflict with their System.Object counterparts.
/// </summary>
public partial class ConflictRenamingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Conflict Renaming";
    public override string Id => "conflictrenamer";

    private const int EqualsLength = 6; // "Equals".Length
    private const int ToStringLength = 8; // "ToString".Length
    private const int GetHashCodeLength = 11; // "GetHashCode".Length
    private const int FinalizeLength = 8; // "Finalize".Length

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        List<MethodAnalysisContext> equalsMethods = [];
        List<MethodAnalysisContext> toStringMethods = [];
        List<MethodAnalysisContext> getHashCodeMethods = [];
        List<MethodAnalysisContext> finalizeMethods = [];

        HashSet<int> equalsUnderscoreCount = [];
        HashSet<int> toStringUnderscoreCount = [];
        HashSet<int> getHashCodeUnderscoreCount = [];
        HashSet<int> finalizeUnderscoreCount = [];

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

                MaybeAddToHashSet(type.Name, equalsUnderscoreCount, toStringUnderscoreCount, getHashCodeUnderscoreCount, finalizeUnderscoreCount);

                foreach (var field in type.Fields)
                {
                    MaybeAddToHashSet(field.Name, equalsUnderscoreCount, toStringUnderscoreCount, getHashCodeUnderscoreCount, finalizeUnderscoreCount);
                }

                foreach (var property in type.Properties)
                {
                    MaybeAddToHashSet(property.Name, equalsUnderscoreCount, toStringUnderscoreCount, getHashCodeUnderscoreCount, finalizeUnderscoreCount);
                }

                foreach (var @event in type.Events)
                {
                    MaybeAddToHashSet(@event.Name, equalsUnderscoreCount, toStringUnderscoreCount, getHashCodeUnderscoreCount, finalizeUnderscoreCount);
                }

                foreach (var method in type.Methods)
                {
                    var name = method.Name;
                    switch (name)
                    {
                        case "Equals":
                            if (method.Parameters.Count == 1 && method.Parameters[0].ParameterType == il2CppSystemObject && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                equalsMethods.Add(method);
                            }
                            break;
                        case "ToString":
                            if (method.Parameters.Count == 0 && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                toStringMethods.Add(method);
                            }
                            break;
                        case "GetHashCode":
                            if (method.Parameters.Count == 0 && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                getHashCodeMethods.Add(method);
                            }
                            break;
                        case "Finalize":
                            if (method.Parameters.Count == 0 && method.GenericParameters.Count == 0)
                            {
                                Debug.Assert(!method.IsInjected);
                                finalizeMethods.Add(method);
                            }
                            break;
                        default:
                            MaybeAddToHashSet(name, equalsUnderscoreCount, toStringUnderscoreCount, getHashCodeUnderscoreCount, finalizeUnderscoreCount);
                            break;
                    }
                }
            }

            progressCallback?.Invoke(i, appContext.Assemblies.Count);
        }

        // Equals
        var newEqualsName = GetNewName("Equals", equalsUnderscoreCount);
        foreach (var method in equalsMethods)
        {
            method.OverrideName = newEqualsName;
        }

        // ToString
        var newToStringName = GetNewName("ToString", toStringUnderscoreCount);
        foreach (var method in toStringMethods)
        {
            method.OverrideName = newToStringName;
        }

        // GetHashCode
        var newGetHashCodeName = GetNewName("GetHashCode", getHashCodeUnderscoreCount);
        foreach (var method in getHashCodeMethods)
        {
            method.OverrideName = newGetHashCodeName;
        }

        // Finalize
        var newFinalizeName = GetNewName("Finalize", finalizeUnderscoreCount);
        foreach (var method in finalizeMethods)
        {
            method.OverrideName = newFinalizeName;
        }
    }

    private static void MaybeAddToHashSet(string name, HashSet<int> equalsUnderscoreCount, HashSet<int> toStringUnderscoreCount, HashSet<int> getHashCodeUnderscoreCount, HashSet<int> finalizeUnderscoreCount)
    {
        if (EqualsRegex.IsMatch(name))
        {
            equalsUnderscoreCount.Add(name.Length - EqualsLength);
        }
        else if (ToStringRegex.IsMatch(name))
        {
            toStringUnderscoreCount.Add(name.Length - ToStringLength);
        }
        else if (GetHashCodeRegex.IsMatch(name))
        {
            getHashCodeUnderscoreCount.Add(name.Length - GetHashCodeLength);
        }
        else if (FinalizeRegex.IsMatch(name))
        {
            finalizeUnderscoreCount.Add(name.Length - FinalizeLength);
        }
    }

    private static string GetNewName(string baseName, HashSet<int> existingUnderscoreCounts)
    {
        var underscoreCount = GetNextAvailableUnderscoreCount(existingUnderscoreCounts);
        return baseName + new string('_', underscoreCount);
    }

    private static int GetNextAvailableUnderscoreCount(HashSet<int> existingUnderscoreCounts)
    {
        var count = 1;
        while (existingUnderscoreCounts.Contains(count))
        {
            count++;
        }
        return count;
    }

    [GeneratedRegex(@"^Equals_+$")]
    private static partial Regex EqualsRegex { get; }

    [GeneratedRegex(@"^ToString_+$")]
    private static partial Regex ToStringRegex { get; }

    [GeneratedRegex(@"^GetHashCode_+$")]
    private static partial Regex GetHashCodeRegex { get; }

    [GeneratedRegex(@"^Finalize_+$")]
    private static partial Regex FinalizeRegex { get; }
}
