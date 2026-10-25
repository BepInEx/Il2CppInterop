using System.CodeDom.Compiler;
using Microsoft.CodeAnalysis;

namespace Il2CppInterop.SourceGenerator;

internal static class Extensions
{
    // Converts a Roslyn Accessibility to the keyword(s) needed in emitted source.
    // Protected/ProtectedAndInternal are included for completeness but are unusual
    // on a top-level injected type.
    internal static string GetAccessibilityKeyword(this Accessibility a) => a switch
    {
        Accessibility.Public => "public",
        Accessibility.Internal => "internal",
        Accessibility.Private => "private",
        Accessibility.Protected => "protected",
        Accessibility.ProtectedOrInternal => "protected internal",
        Accessibility.ProtectedAndInternal => "private protected",
        _ => "public",
    };

    internal static string GetTypeKeyword(this TypeKind kind) => kind switch
    {
        TypeKind.Class => "class",
        TypeKind.Struct => "struct",
        TypeKind.Interface => "interface",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    extension(INamedTypeSymbol? symbol)
    {
        public bool IsType(string name, ReadOnlySpan<string> namespaceParts) =>
            symbol is not null &&
            symbol.Name == name &&
            MatchesNamespace(symbol.ContainingNamespace, namespaceParts);

        public bool HasAttribute(string name, ReadOnlySpan<string> namespaceParts) =>
            ((ISymbol?)symbol).HasAttribute(name, namespaceParts);
    }

    extension(ISymbol? symbol)
    {
        public bool HasAttribute(string name, ReadOnlySpan<string> namespaceParts)
        {
            if (symbol is null)
                return false;

            foreach (var attr in symbol.GetAttributes())
            {
                if (attr.AttributeClass.IsType(name, namespaceParts))
                    return true;
            }

            return false;
        }
    }

    private static bool MatchesNamespace(INamespaceSymbol? ns, ReadOnlySpan<string> parts)
    {
        // Walk from innermost to outermost
        for (var i = parts.Length - 1; i >= 0; i--)
        {
            if (ns is null or { IsGlobalNamespace: true } || ns.Name != parts[i])
                return false;
            ns = ns.ContainingNamespace;
        }

        // Make sure we've consumed all namespaces up to global
        return ns is null or { IsGlobalNamespace: true };
    }

    internal static void WriteLineNoTabs(this IndentedTextWriter writer) => writer.WriteLineNoTabs(null);

}
