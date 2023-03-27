using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Il2CppInterop.Analyzers;

public static class Utilities
{
    public static bool IsIl2CppObject(SyntaxNodeAnalysisContext context, ITypeSymbol typeSymbol)
    {
        var il2CppObjectType = context.Compilation.GetTypeByMetadataName("Il2CppSystem.Object");
        return IsIl2CppObjectInternal(typeSymbol, il2CppObjectType);
    }

    private static bool IsIl2CppObjectInternal(ITypeSymbol typeSymbol, INamedTypeSymbol il2CppObjectType)
    {
        return typeSymbol.Equals(il2CppObjectType, SymbolEqualityComparer.Default) ||
               (typeSymbol.BaseType != null && IsIl2CppObjectInternal(typeSymbol.BaseType, il2CppObjectType));
    }
}
