using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Il2CppInterop.SourceGenerator.Tests.Diagnostics;

/// <summary>
/// Tests for the <see cref="InjectedTypeAnalyzer.MustBePartial"/> diagnostic, which is raised when a type marked with [InjectedType] is not also marked as partial.
/// </summary>
public class PartialTypeAnalyzerTests : AnalyzerTests
{
    [Test]
    public Task DiagnosticWhenTypeNotPartial()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public class Sample : Il2CppSystem.Object
            {
            }
            """;

        var expectedDiagnostic = new DiagnosticResult(InjectedTypeAnalyzer.MustBePartial)
            .WithSpan(LinePositionSpan.FindInSource(testCode, "Sample"))
            .WithArguments("Sample");

        return TestDiagnostic(testCode, expectedDiagnostic);
    }

    [Test]
    public Task NoDiagnosticsWhenTypeIsCorrectlyMarkedPartial()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public partial class Sample : Il2CppSystem.Object
            {
            }
            """;

        return TestNoDiagnostics(testCode);
    }
}
