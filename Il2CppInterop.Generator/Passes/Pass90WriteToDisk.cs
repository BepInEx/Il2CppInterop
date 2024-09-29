using System.Runtime.Versioning;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Builder;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass90WriteToDisk
{
    public static void DoPass(RewriteGlobalContext context, GeneratorOptions options)
    {
        var targetAttributeConstructor = typeof(TargetFrameworkAttribute).GetConstructor([typeof(string)]);

        foreach (var asmContext in context.Assemblies)
        {
            var module = asmContext.NewAssembly.ManifestModule!;

            // Rewrite corlib references.
            foreach (var reference in module.AssemblyReferences)
            {
                // System.Private.CoreLib needs rewriting because references can get created during the rewrite process.
                // mscorlib and netstandard are included for completeness.
                if (reference.Name?.Value is "System.Private.CoreLib" or "mscorlib" or "netstandard")
                {
                    CorlibReferences.RewriteCorlibReference(reference);
                    continue;
                }
            }

            // Add TargetFrameworkAttribute to the assembly.
            {
                var importedConstructor = (ICustomAttributeType)module.DefaultImporter.ImportMethod(targetAttributeConstructor);

                CustomAttribute targetFrameworkAttribute = new(importedConstructor, new());

                CustomAttributeArgument fixedArgument = new(module.CorLibTypeFactory.String, module.OriginalTargetRuntime.ToString());
                targetFrameworkAttribute.Signature!.FixedArguments.Add(fixedArgument);

                CustomAttributeNamedArgument namedArgument = new(
                    CustomAttributeArgumentMemberType.Property,
                    nameof(TargetFrameworkAttribute.FrameworkDisplayName),
                    module.CorLibTypeFactory.String,
                    new(module.CorLibTypeFactory.String, CorlibReferences.TargetFrameworkName));
                targetFrameworkAttribute.Signature.NamedArguments.Add(namedArgument);

                asmContext.NewAssembly.CustomAttributes.Add(targetFrameworkAttribute);
            }

            // Optimize macros in all methods and assign tokens.
            foreach (var type in module.GetAllTypes())
            {
                foreach (var method in type.Methods)
                {
                    method.CilMethodBody?.Instructions.OptimizeMacros();
                    module.TokenAllocator.AssignNextAvailableToken(method);
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
