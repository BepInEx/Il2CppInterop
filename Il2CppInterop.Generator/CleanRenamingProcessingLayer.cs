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

            if (GenericTypeName.TryMatch(type.Name, out var typeName, out var genericCount))
            {
                type.OverrideName = $"{typeName.MakeValidCSharpName()}`{genericCount}";
            }
            else if (type.IsModuleType)
            {
                type.OverrideName = $"Module_{type.DeclaringAssembly.DefaultName.MakeValidCSharpName()}";
            }
            else if (type.IsPrivateImplementationDetailsType)
            {
                type.OverrideName = $"PrivateImplementationDetails_{type.DeclaringAssembly.DefaultName.MakeValidCSharpName()}";
            }
            else
            {
                type.OverrideName = type.Name.MakeValidCSharpName();
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
        // Virtual method lookup depends on the name being consistent with the base type.
        // Special names also have special meaning and should not be changed.
        const MethodAttributes FlagsWhichRequireNameConsistency =
            MethodAttributes.SpecialName |
            MethodAttributes.Abstract |
            MethodAttributes.Virtual |
            MethodAttributes.RTSpecialName;

        var typeName = GetCSharpName(type);

        // Events and properties do not get renamed

        // Collect all reserved names
        HashSet<string> reservedNames =
        [
            "",
            "_",
            typeName,
            .. GetConflictingNames(type.Properties),
            .. GetConflictingNames(type.Events),
        ];
        if (StartsWithGetSet(typeName))
        {
            reservedNames.Add(typeName.Substring(4));
        }

        HashSet<(string Name, int SignatureHash)> existingMethods = [];

        // Injected and special methods
        foreach (var method in type.Methods)
        {
            if (method.IsInjected)
            {
                existingMethods.Add((method.Name, GetMethodSignatureHash(method)));
            }
            else if ((method.Attributes & FlagsWhichRequireNameConsistency) != 0)
            {
                if (method.IsStaticConstructor)
                {
                    // Rename static constructor, but allow it to be renamed again if needed.
                    // Also remove special name flags, since it's no longer a special name.
                    method.OverrideName = "StaticConstructor";
                    method.OverrideAttributes = method.Attributes & ~(MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
                }
                else
                {
                    existingMethods.Add((method.Name, GetMethodSignatureHash(method)));
                }
            }
        }

        // Normal methods
        foreach (var method in type.Methods)
        {
            if (method.IsInjected)
                continue;

            if ((method.Attributes & FlagsWhichRequireNameConsistency) != 0)
                continue;

            var methodName = method.Name.MakeValidCSharpName();
            var signatureHash = GetMethodSignatureHash(method);

            while (reservedNames.Contains(methodName) || !existingMethods.Add((methodName, signatureHash)))
            {
                methodName = $"_{methodName}";
            }
            method.OverrideName = methodName;
        }

        reservedNames.AddRange(GetConflictingNames(type.Methods));

        // Fields
        foreach (var field in type.Fields)
        {
            if (field.IsInjected)
                continue;

            string fieldName;
            if (TryMatchPropertyBackingField(field.Name, out var propertyName))
            {
                fieldName = $"{propertyName}_BackingField";
            }
            else if (type.Events.Any(e => e.Name == field.Name))
            {
                fieldName = $"{field.Name}_BackingField";
            }
            else
            {
                fieldName = field.Name.MakeValidCSharpName();
            }

            while (reservedNames.Contains(fieldName))
            {
                fieldName = $"_{fieldName}";
            }
            field.OverrideName = fieldName;
            reservedNames.Add(field.Name);
        }

        // Parameters
        var parameterNames = new HashSet<string>();
        foreach (var method in type.Methods)
        {
            if (method.IsInjected)
                continue;

            if (method.Parameters.Count == 0)
                continue;

            parameterNames.Clear();

            for (var i = 0; i < method.Parameters.Count; i++)
            {
                var parameter = method.Parameters[i];
                var parameterName = string.IsNullOrEmpty(parameter.Name)
                    ? $"parameter{i}"
                    : parameter.Name.MakeValidCSharpName();

                while (reservedNames.Contains(parameterName) || !parameterNames.Add(parameterName))
                {
                    parameterName = $"_{parameterName}";
                }
                parameter.OverrideName = parameterName;
            }
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

    private static bool StartsWithGetSet(string name)
    {
        return name.StartsWith("get_", StringComparison.Ordinal)
            || name.StartsWith("set_", StringComparison.Ordinal);
    }

    private static string GetCSharpName(TypeAnalysisContext type)
    {
        if (GenericTypeName.TryMatch(type.Name, out var typeName, out _))
        {
            return typeName;
        }
        else
        {
            return type.Name;
        }
    }

    private static int GetMethodSignatureHash(MethodAnalysisContext method)
    {
        HashCode hash = new();
        hash.Add(method.GenericParameters.Count);
        hash.Add(TypeAnalysisContextEqualityComparer.Instance.GetHashCode(method.ReturnType));
        foreach (var param in method.Parameters)
        {
            hash.Add(TypeAnalysisContextEqualityComparer.Instance.GetHashCode(param.ParameterType));
        }
        return hash.ToHashCode();
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
}
