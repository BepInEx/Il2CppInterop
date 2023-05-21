using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.Analyzers;

public static class Utilities
{
    private static readonly RecursivePatternSyntax s_formattedRecursive = SyntaxFactory.ParseExpression("o is { } ")
        .DescendantNodes().OfType<RecursivePatternSyntax>().First();

    public static bool IsIl2CppObject(SyntaxNodeAnalysisContext context, ITypeSymbol typeSymbol)
    {
        var il2CppObjectType = context.Compilation.GetTypeByMetadataName("Il2CppSystem.Object");
        return IsIl2CppObjectInternal(typeSymbol, il2CppObjectType!);
    }

    public static TypeSyntax? GetTypeFromPattern(ExpressionOrPatternSyntax pattern)
    {
        return pattern switch
        {
            ConstantPatternSyntax constant => constant.Expression as TypeSyntax,
            DeclarationPatternSyntax declaration => declaration.Type,
            RecursivePatternSyntax recursive => recursive.Type,
            UnaryPatternSyntax unary => GetTypeFromPattern(unary.Pattern),
            _ => null
        };
    }

    public static PatternSyntax? RemoveTypeFromPattern(ExpressionOrPatternSyntax pattern)
    {
        return pattern switch
        {
            ConstantPatternSyntax => s_formattedRecursive,
            DeclarationPatternSyntax declaration => s_formattedRecursive.WithDesignation(declaration.Designation),
            RecursivePatternSyntax recursive => recursive.WithType(null),
            UnaryPatternSyntax unary => unary.WithPattern(RemoveTypeFromPattern(unary.Pattern)!),
            _ => null
        };
    }

    private static bool IsIl2CppObjectInternal(ITypeSymbol typeSymbol, INamedTypeSymbol il2CppObjectType)
    {
        return typeSymbol.Equals(il2CppObjectType, SymbolEqualityComparer.Default) ||
               (typeSymbol.BaseType != null && IsIl2CppObjectInternal(typeSymbol.BaseType, il2CppObjectType));
    }
}
