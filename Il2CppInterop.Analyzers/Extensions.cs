using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Il2CppInterop.Analyzers;

public static class Extensions
{
    public static SeparatedSyntaxList<T> ToSeparatedSyntaxList<T>(this T item) where T : SyntaxNode
    {
        return SyntaxFactory.SingletonSeparatedList(item);
    }
}
