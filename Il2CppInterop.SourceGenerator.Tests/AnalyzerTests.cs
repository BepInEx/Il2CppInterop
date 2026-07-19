using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;

namespace Il2CppInterop.SourceGenerator.Tests;

public abstract class AnalyzerTests
{
    protected CSharpAnalyzerTest<InjectedTypeAnalyzer, DefaultVerifier> Context { get; private set; }

    [SetUp]
    public void Setup()
    {
        Context = new()
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net100
        };
        Context.TestState.AdditionalReferences.Add(typeof(InjectedTypeAttribute).Assembly);
        Context.TestState.AdditionalReferences.Add(typeof(Il2CppSystem.Object).Assembly);
        Context.TestState.AdditionalReferences.Add(typeof(RuntimeInvoke).Assembly);
    }

    protected async Task TestDiagnostic(string testCode, DiagnosticResult expectedDiagnostic)
    {
        Context.TestCode = testCode;
        Context.ExpectedDiagnostics.Add(expectedDiagnostic);
        await Context.RunAsync(TestContext.CurrentContext.CancellationToken);
    }

    protected async Task TestDiagnostics(string testCode, params IEnumerable<DiagnosticResult> expectedDiagnostics)
    {
        Context.TestCode = testCode;
        Context.ExpectedDiagnostics.AddRange(expectedDiagnostics);
        await Context.RunAsync(TestContext.CurrentContext.CancellationToken);
    }

    protected async Task TestNoDiagnostics(string testCode)
    {
        Context.TestCode = testCode;
        await Context.RunAsync(TestContext.CurrentContext.CancellationToken);
    }
}
