using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Il2CppInterop.SourceGenerator.Tests;

internal static class DiagnosticResultExtensions
{
    public static DiagnosticResult WithSpan(this DiagnosticResult result, LinePosition start, LinePosition end)
    {
        return result.WithSpan(start.Line, start.Character, end.Line, end.Character);
    }

    public static DiagnosticResult WithSpan(this DiagnosticResult result, LinePositionSpan span)
    {
        return result.WithSpan(span.Start, span.End);
    }
}
