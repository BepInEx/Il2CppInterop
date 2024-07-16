using AsmResolver;
using AsmResolver.DotNet.Builder;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass90WriteToDisk
{
    public static void DoPass(RewriteGlobalContext context, GeneratorOptions options)
    {
        foreach (var asmContext in context.Assemblies)
        {
            var module = asmContext.NewAssembly.ManifestModule!;
            foreach (var reference in module.AssemblyReferences)
            {
                // System.Private.CoreLib needs rewriting because references can get created during the rewrite process.
                // mscorlib needs rewriting because we initially set 2.0.0.0 as the version for resolving references.
                if (reference.Name?.Value is "System.Private.CoreLib" or "mscorlib")
                {
                    CorlibReferences.RewriteReferenceToMscorlib(reference);
                    continue;
                }
            }
        }

        var assembliesToProcess = context.Assemblies
            .Where(it => !options.AdditionalAssembliesBlacklist.Contains(it.NewAssembly.Name!));

        void Processor(AssemblyRewriteContext assemblyContext)
        {
            assemblyContext.NewAssembly.Write(
                Path.Combine(options.OutputDir ?? ".", $"{assemblyContext.NewAssembly.Name}.dll"), new ManagedPEImageBuilder(ThrowErrorListener.Instance));
        }

        if (options.Parallel)
        {
            Parallel.ForEach(assembliesToProcess, Processor);
        }
        else
        {
            foreach (var assemblyRewriteContext in assembliesToProcess)
            {
                Processor(assemblyRewriteContext);
            }
        }
    }
}
