using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass10CreateTypedefs
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var type in assemblyContext.OriginalAssembly.ManifestModule!.TopLevelTypes)
                if (!IsCpp2ILInjectedType(type) && type.Name != "<Module>")
                    ProcessType(type, assemblyContext, null);

        static bool IsCpp2ILInjectedType(TypeDefinition type) => type.Namespace?.Value.StartsWith("Cpp2ILInjected", StringComparison.Ordinal) ?? false;
    }

    private static void ProcessType(TypeDefinition type, AssemblyRewriteContext assemblyContext,
        TypeDefinition? parentType)
    {
        var convertedTypeName = GetConvertedTypeName(assemblyContext.GlobalContext, type, parentType);
        var newType =
            new TypeDefinition(
                convertedTypeName.Namespace ?? GetNamespace(type, assemblyContext),
                convertedTypeName.Name, AdjustAttributes(type.Attributes));
        newType.IsSequentialLayout = false;

        if (type.IsSealed && type.IsAbstract) // is static
            newType.IsSealed = newType.IsAbstract = true;

        if (parentType == null)
        {
            assemblyContext.NewAssembly.ManifestModule!.TopLevelTypes.Add(newType);
        }
        else
        {
            parentType.NestedTypes.Add(newType);
        }

        foreach (var typeNestedType in type.NestedTypes)
            ProcessType(typeNestedType, assemblyContext, newType);

        assemblyContext.RegisterTypeRewrite(new TypeRewriteContext(assemblyContext, type, newType));

        static string? GetNamespace(TypeDefinition type, AssemblyRewriteContext assemblyContext)
        {
            if (type.Name?.Value is "<Module>" || type.DeclaringType is not null)
                return type.Namespace;
            else
                return type.Namespace.UnSystemify(assemblyContext.GlobalContext.Options);
        }
    }

    internal static (string? Namespace, string Name) GetConvertedTypeName(
        RewriteGlobalContext assemblyContextGlobalContext, TypeDefinition type, TypeDefinition? enclosingType)
    {
        if (assemblyContextGlobalContext.Options.PassthroughNames)
            return (null, type.Name!);

        if (type.Name.IsObfuscated(assemblyContextGlobalContext.Options))
        {
            var newNameBase = assemblyContextGlobalContext.RenamedTypes[type];
            var genericParametersCount = type.GenericParameters.Count;
            var renameGroup =
                assemblyContextGlobalContext.RenameGroups[
                    ((object?)type.DeclaringType ?? type.Namespace, newNameBase, genericParametersCount)];
            var genericSuffix = genericParametersCount == 0 ? "" : "`" + genericParametersCount;
            var convertedTypeName = newNameBase +
                                    (renameGroup.Count == 1 ? "Unique" : renameGroup.IndexOf(type).ToString()) +
                                    genericSuffix;

            var fullName = enclosingType == null
                ? type.Namespace
                : enclosingType.GetNamespacePrefix() + "." + enclosingType.Name;

            if (assemblyContextGlobalContext.Options.RenameMap.TryGetValue(fullName + "." + convertedTypeName,
                    out var newName))
            {
                if (type.Module!.TopLevelTypes.Any(t => t.FullName == newName))
                {
                    Logger.Instance.LogWarning("[Rename map issue] {NewName} already exists in {ModuleName} (mapped from {MappedNamespace}.{MappedType})",
                        newName, type.Module.Name, fullName, convertedTypeName);
                    newName += "_Duplicate";
                }

                var lastDotPosition = newName.LastIndexOf(".");
                if (lastDotPosition >= 0)
                {
                    var ns = newName.Substring(0, lastDotPosition);
                    var name = newName.Substring(lastDotPosition + 1);
                    return (ns, name);
                }

                convertedTypeName = newName;
            }

            return (null, convertedTypeName);
        }

        return (null, type.Name.MakeValidInSource());
    }

    private static TypeAttributes AdjustAttributes(TypeAttributes typeAttributes)
    {
        typeAttributes |= TypeAttributes.BeforeFieldInit;
        typeAttributes &= ~(TypeAttributes.Abstract | TypeAttributes.Interface);

        var visibility = typeAttributes & TypeAttributes.VisibilityMask;
        if (visibility == 0 || visibility == TypeAttributes.Public)
            return typeAttributes | TypeAttributes.Public;

        return (typeAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;
    }
}
