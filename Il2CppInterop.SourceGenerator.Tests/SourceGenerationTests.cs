using System.Text;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

namespace Il2CppInterop.SourceGenerator.Tests;

public abstract class SourceGenerationTests
{
    protected CSharpSourceGeneratorTest<InjectedTypeGenerator, DefaultVerifier> Context { get; private set; }

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

    protected async Task TestGeneration(string testCode, string expectedFileName, string expectedGeneratedCode)
    {
        Context.TestCode = testCode;
        Context.TestState.GeneratedSources.Add((typeof(InjectedTypeGenerator), expectedFileName, SourceText.From(expectedGeneratedCode.Replace("\r", null), Encoding.UTF8, SourceHashAlgorithm.Sha256)));
        await Context.RunAsync(TestContext.CurrentContext.CancellationToken);
    }
}
