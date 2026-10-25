using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Il2CppInterop.SourceGenerator.Tests.Diagnostics;

/// <summary>
/// Tests for the <see cref="InjectedTypeAnalyzer.ShouldNotHaveUninjectedInstanceFields"/> diagnostic, which is raised when a struct marked with [InjectedType] contains instance fields that are not marked with [Il2CppField].
/// </summary>
public class UninjectedInstanceFieldAnalyzerTests : AnalyzerTests
{
    [Test]
    public Task NoDiagnosticsForClassContainingNoInstanceFields()
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

    [Test]
    public Task NoDiagnosticsForClassContainingStaticField()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public partial class Sample : Il2CppSystem.Object
            {
                public static int value;
            }
            """;

        return TestNoDiagnostics(testCode);
    }

    [Test]
    public Task DiagnosticForClassContainingUninjectedInstanceField()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public partial class Sample : Il2CppSystem.Object
            {
                public int value;
            }
            """;

        var expectedDiagnostic = new DiagnosticResult(InjectedTypeAnalyzer.ShouldNotHaveUninjectedInstanceFields)
            .WithSpan(LinePositionSpan.FindInSource(testCode, "value"))
            .WithArguments("Sample", "value");

        return TestDiagnostic(testCode, expectedDiagnostic);
    }

    [Test]
    public Task NoDiagnosticsForStructContainingNoInstanceFields()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public partial struct Sample
            {
            }
            """;

        return TestNoDiagnostics(testCode);
    }

    [Test]
    public Task NoDiagnosticsForStructContainingInstanceFieldsWithIl2CppFieldAttributes()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public partial struct Sample
            {
                [Il2CppField]
                public Il2CppSystem.Int32 intValue;
                [Il2CppField]
                public Il2CppSystem.String stringValue;
            }
            """;

        return TestNoDiagnostics(testCode);
    }

    [Test]
    public Task NoDiagnosticsForStructContainingInstanceFieldsWithoutIl2CppFieldAttributes()
    {
        var testCode = """
            using Il2CppInterop.Common.Attributes;
            [InjectedType]
            public partial struct Sample
            {
                public Il2CppSystem.Int32 intValue;
                public Il2CppSystem.String stringValue;
            }
            """;

        return TestNoDiagnostics(testCode);
    }
}
