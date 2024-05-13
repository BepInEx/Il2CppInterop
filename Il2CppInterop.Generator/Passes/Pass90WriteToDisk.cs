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
            var module = asmContext.NewAssembly.ManifestModule;
            foreach (var reference in module.AssemblyReferences)
            {
                // TODO: Instead of a hack, set correctly initially via source generator
                if (reference.Name == "System.Private.CoreLib")
                {
                    CorlibReferences.RewriteReferenceToMscorlib(reference);
                    continue;
                }
            }
        }

        var assembliesToProcess = context.Assemblies
            .Where(it => !options.AdditionalAssembliesBlacklist.Contains(it.NewAssembly.Name));

        void Processor(AssemblyRewriteContext assemblyContext)
        {
            try
            {
                assemblyContext.NewAssembly.Write(
                    Path.Combine(options.OutputDir ?? ".", $"{assemblyContext.NewAssembly.Name}.dll"), new ManagedPEImageBuilder(ThrowErrorListener.Instance));
            }
            catch (Exception e)
            {
                throw e;
            }
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
