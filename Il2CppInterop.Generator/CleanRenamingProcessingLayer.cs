using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.RegularExpressions;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public partial class CleanRenamingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Clean Name Changes";

    public override string Id => "cleanrenamer";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        for (var i = 0; i < appContext.Assemblies.Count; i++)
        {
            var assembly = appContext.Assemblies[i];

            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            RenameTypes(assembly.TopLevelTypes);

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                RenameMembers(type);
            }

            progressCallback?.Invoke(i, appContext.Assemblies.Count);
        }
    }

    private static void RenameTypes(IEnumerable<TypeAnalysisContext> types, string? declaringTypeName = null)
    {
        HashSet<(string, string)> reservedFullNames = [];
        foreach (var type in types)
        {
            if (type.IsInjected)
            {
                reservedFullNames.Add((type.Namespace, type.Name));
                continue;
            }

            if (TryMatchGenericType(type.Name, out var typeName, out var genericCount))
            {
                type.OverrideName = $"{ReplaceInvalidWithUnderscore(typeName)}`{genericCount}";
            }
            else if (type.IsModuleType)
            {
                type.OverrideName = $"Module_{ReplaceInvalidWithUnderscore(type.DeclaringAssembly.DefaultName)}";
            }
            else if (type.IsPrivateImplementationDetailsType)
            {
                type.OverrideName = $"PrivateImplementationDetails_{ReplaceInvalidWithUnderscore(type.DeclaringAssembly.DefaultName)}";
            }
            else
            {
                type.OverrideName = ReplaceInvalidWithUnderscore(type.Name);
            }
        }

        // Resolve any name conflicts
        foreach (var type in types)
        {
            if (type.IsInjected)
                continue;

            var @namespace = type.Namespace;
            var name = type.Name;
            while (name == declaringTypeName || !reservedFullNames.Add((@namespace, name)))
            {
                name = $"_{name}";
            }

            type.OverrideName = name;
        }

        foreach (var type in types)
        {
            RenameTypes(type.NestedTypes, type.Name);
        }
    }

    private static void RenameMembers(TypeAnalysisContext type)
    {
        const MethodAttributes FlagsWhichRequireNameConsistency =
            MethodAttributes.SpecialName |
            MethodAttributes.Abstract |
            MethodAttributes.Virtual |
            MethodAttributes.RTSpecialName;

        // Fields
        foreach (var field in type.Fields)
        {
            if (field.IsInjected)
                continue;

            if (TryMatchPropertyBackingField(field.Name, out var propertyName))
            {
                field.OverrideName = $"{propertyName}_BackingField";
            }
            else if (type.Events.Any(e => e.Name == field.Name))
            {
                field.OverrideName = $"{field.Name}_BackingField";
            }
            else
            {
                field.OverrideName = ReplaceInvalidWithUnderscore(field.Name);
            }
        }

        // Methods
        foreach (var method in type.Methods)
        {
            if (method.IsInjected)
                continue;

            if ((method.Attributes & FlagsWhichRequireNameConsistency) != 0)
                continue;

            method.OverrideName = ReplaceInvalidWithUnderscore(method.Name);
        }

        // Collect all reserved names
        HashSet<string> reservedNames =
        [
            "",
            "_",
            type.Name,
            .. GetConflictingNames(type.Properties),
            .. GetConflictingNames(type.Events),
            .. GetConflictingNames(type.Methods),
        ];
        if (StartsWithGetSet(type.Name))
        {
            reservedNames.Add(type.Name.Substring(4));
        }

        // Resolve any name conflicts for fields
        foreach (var field in type.Fields)
        {
            if (field.IsInjected)
                continue;

            field.OverrideName = PrependUnderscoreIfConflicting(field.Name, reservedNames);
            reservedNames.Add(field.Name);
        }

        // Rename static constructors
        foreach (var method in type.Methods)
        {
            if (method.IsInjected)
                continue;

            if (!method.IsStaticConstructor)
                continue;

            method.OverrideName = PrependUnderscoreIfConflicting("StaticConstructor", reservedNames);
            method.OverrideAttributes = method.Attributes & ~(MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
            reservedNames.Add(method.Name);
        }
    }

    private static IEnumerable<string> GetConflictingNames(IEnumerable<HasCustomAttributesAndName> members)
    {
        foreach (var member in members)
        {
            yield return member.Name;
            if (StartsWithGetSet(member.Name))
            {
                yield return member.Name.Substring(4);
            }
        }
    }

    private static string PrependUnderscoreIfConflicting(string name, HashSet<string> reservedNames)
    {
        while (reservedNames.Contains(name))
        {
            name = $"_{name}";
        }
        return name;
    }

    private static bool StartsWithGetSet(string name)
    {
        return name.StartsWith("get_", StringComparison.Ordinal)
            || name.StartsWith("set_", StringComparison.Ordinal);
    }

    private static string ReplaceInvalidWithUnderscore(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var array = ArrayPool<char>.Shared.Rent(name.Length + 1);
        array[0] = '_';
        name.AsSpan().CopyTo(array.AsSpan(1));
        for (var i = name.Length; i > 0; i--)
        {
            if (!char.IsLetterOrDigit(array[i]))
            {
                array[i] = '_';
            }
        }
        var result = char.IsDigit(array[1]) ? new string(array.AsSpan(0, name.Length + 1)) : new string(array.AsSpan(1, name.Length));
        ArrayPool<char>.Shared.Return(array);
        return result;
    }

    private static bool TryMatchPropertyBackingField(string fieldName, [NotNullWhen(true)] out string? propertyName)
    {
        var match = PropertyBackingFieldRegex.Match(fieldName);
        if (match.Success)
        {
            propertyName = match.Groups[1].Value;
            return true;
        }
        else
        {
            propertyName = null;
            return false;
        }
    }

    [GeneratedRegex(@"^<(\w+)>k__BackingField$")]
    private static partial Regex PropertyBackingFieldRegex { get; }

    private static bool TryMatchGenericType(string genericName, [NotNullWhen(true)] out string? typeName, [NotNullWhen(true)] out string? genericCount)
    {
        var match = GenericTypeRegex.Match(genericName);
        if (match.Success)
        {
            typeName = match.Groups[1].Value;
            genericCount = match.Groups[2].Value;
            return true;
        }
        else
        {
            typeName = null;
            genericCount = null;
            return false;
        }
    }

    [GeneratedRegex(@"^(.+)`(\d+)$")]
    private static partial Regex GenericTypeRegex { get; }
}
