using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Passes;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Runners;

public static class DeobfuscationAnalyzer
{
    public static Il2CppInteropGenerator AddDeobfuscationAnalyzer(this Il2CppInteropGenerator gen)
    {
        return gen.AddRunner<DeobfuscationAnalyzerRunner>();
    }
}

internal class DeobfuscationAnalyzerRunner : IRunner
{
    public void Dispose() { }

    public void Run(GeneratorOptions options)
    {
        RewriteGlobalContext rewriteContext;
        IIl2CppMetadataAccess inputAssemblies;
        using (new TimingCookie("Reading assemblies"))
        {
            inputAssemblies = new AssemblyMetadataAccess(options.Source ?? throw new ArgumentException("Source assemblies must be provided.", nameof(options)));
        }

        using (new TimingCookie("Creating assembly contexts"))
        {
            rewriteContext = new RewriteGlobalContext(options, inputAssemblies, NullMetadataAccess.Instance);
        }

        for (var chars = 1; chars <= 3; chars++)
            for (var uniq = 3; uniq <= 15; uniq++)
            {
                options.TypeDeobfuscationCharsPerUniquifier = chars;
                options.TypeDeobfuscationMaxUniquifiers = uniq;

                rewriteContext.RenamedTypes.Clear();
                rewriteContext.RenameGroups.Clear();

                Pass05CreateRenameGroups.DoPass(rewriteContext);

                var uniqueTypes = rewriteContext.RenameGroups.Values.Count(it => it.Count == 1);
                var nonUniqueTypes = rewriteContext.RenameGroups.Values.Count(it => it.Count > 1);

                // Ensure the output is written to stdout
                Console.WriteLine($"Chars=\t{chars}\tMaxU=\t{uniq}\tUniq=\t{uniqueTypes}\tNonUniq=\t{nonUniqueTypes}");
            }
    }
}
