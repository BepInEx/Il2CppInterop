using AsmResolver.DotNet;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Passes;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Runners;

public static class InteropAssemblyGenerator
{
    public static Il2CppInteropGenerator AddInteropAssemblyGenerator(this Il2CppInteropGenerator gen)
    {
        return gen.AddRunner<InteropAssemblyGeneratorRunner>();
    }
}

internal class InteropAssemblyGeneratorRunner : IRunner
{
    public void Run(GeneratorOptions options)
    {
        if (options.Source == null || !options.Source.Any())
        {
            Console.WriteLine("No input specified; use -h for help");
            return;
        }

        if (string.IsNullOrEmpty(options.OutputDir))
        {
            Console.WriteLine("No target dir specified; use -h for help");
            return;
        }

        if (!Directory.Exists(options.OutputDir))
            Directory.CreateDirectory(options.OutputDir);

        // Filter source assemblies if incremental mode is enabled
        var sourceAssemblies = options.Source;
        if (!string.IsNullOrEmpty(options.ExistingInteropDir) && options.SkipExistingAssemblies)
        {
            using (new TimingCookie("Filtering existing assemblies"))
            {
                sourceAssemblies = FilterExistingAssemblies(options.Source, options.ExistingInteropDir);
                if (!sourceAssemblies.Any())
                {
                    Logger.Instance.LogInformation("All assemblies already exist in ExistingInteropDir, nothing to generate");
                    return;
                }
                Logger.Instance.LogInformation("Incremental mode: generating {Count} assemblies (skipped {Skipped} existing)",
                    sourceAssemblies.Count, options.Source.Count - sourceAssemblies.Count);
            }
        }

        RewriteGlobalContext rewriteContext;
        IIl2CppMetadataAccess gameAssemblies;
        IMetadataAccess unityAssemblies;

        using (new TimingCookie("Reading assemblies"))
        {
            gameAssemblies = new AssemblyMetadataAccess(sourceAssemblies);
        }

        // Load reference assemblies: UnityBaseLibsDir or ExistingInteropDir
        if (!string.IsNullOrEmpty(options.UnityBaseLibsDir))
            using (new TimingCookie("Reading unity assemblies"))
            {
                unityAssemblies = new AssemblyMetadataAccess(Directory.EnumerateFiles(options.UnityBaseLibsDir, "*.dll"));
            }
        else if (!string.IsNullOrEmpty(options.ExistingInteropDir))
            using (new TimingCookie("Reading existing interop assemblies"))
            {
                unityAssemblies = new AssemblyMetadataAccess(Directory.EnumerateFiles(options.ExistingInteropDir, "*.dll"));
            }
        else
            unityAssemblies = NullMetadataAccess.Instance;

        using (new TimingCookie("Creating rewrite assemblies"))
        {
            rewriteContext = new RewriteGlobalContext(options, gameAssemblies, unityAssemblies);
        }

        using (new TimingCookie("Computing renames"))
        {
            Pass05CreateRenameGroups.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating typedefs"))
        {
            Pass10CreateTypedefs.DoPass(rewriteContext);
        }

        using (new TimingCookie("Computing struct blittability"))
        {
            Pass11ComputeTypeSpecifics.DoPass(rewriteContext);
        }

        using (new TimingCookie("Filling typedefs"))
        {
            Pass12FillTypedefs.DoPass(rewriteContext);
        }

        using (new TimingCookie("Filling generic constraints"))
        {
            Pass13FillGenericConstraints.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating members"))
        {
            Pass15GenerateMemberContexts.DoPass(rewriteContext);
        }

        using (new TimingCookie("Scanning method cross-references"))
        {
            Pass16ScanMethodRefs.DoPass(rewriteContext, options);
        }

        using (new TimingCookie("Finalizing method declarations"))
        {
            Pass18FinalizeMethodContexts.DoPass(rewriteContext);
        }

        Logger.Instance.LogInformation("{DeadMethodsCount} total potentially dead methods", Pass18FinalizeMethodContexts.TotalPotentiallyDeadMethods);

        using (new TimingCookie("Filling method parameters"))
        {
            Pass19CopyMethodParameters.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating static constructors"))
        {
            Pass20GenerateStaticConstructors.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating value type fields"))
        {
            Pass21GenerateValueTypeFields.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating enums"))
        {
            Pass22GenerateEnums.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating IntPtr constructors"))
        {
            Pass23GeneratePointerConstructors.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating non-blittable struct constructors"))
        {
            Pass25GenerateNonBlittableValueTypeDefaultCtors.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating generic method static constructors"))
        {
            Pass30GenerateGenericMethodStoreConstructors.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating field accessors"))
        {
            Pass40GenerateFieldAccessors.DoPass(rewriteContext);
        }

        using (new TimingCookie("Filling methods"))
        {
            Pass50GenerateMethods.DoPass(rewriteContext);
        }

        using (new TimingCookie("Generating implicit conversions"))
        {
            Pass60AddImplicitConversions.DoPass(rewriteContext);
        }

        using (new TimingCookie("Implementing awaiters"))
        {
            Pass61ImplementAwaiters.DoPass(rewriteContext);
        }

        using (new TimingCookie("Creating properties"))
        {
            Pass70GenerateProperties.DoPass(rewriteContext);
        }

        if (options.UnityBaseLibsDir != null)
        {
            using (new TimingCookie("Unstripping types"))
            {
                Pass79UnstripTypes.DoPass(rewriteContext);
            }

            using (new TimingCookie("Unstripping fields"))
            {
                Pass80UnstripFields.DoPass(rewriteContext);
            }

            using (new TimingCookie("Unstripping methods"))
            {
                Pass80UnstripMethods.DoPass(rewriteContext);
            }

            using (new TimingCookie("Unstripping method bodies"))
            {
                Pass81FillUnstrippedMethodBodies.DoPass(rewriteContext);
            }
        }
        else if (options.ExistingInteropDir != null)
        {
            // Incremental mode: skip unstripping as existing interop already has unstripped members
            Logger.Instance.LogInformation("Incremental mode: skipping unstripping (using existing interop as reference)");
        }
        else
        {
            Logger.Instance.LogWarning("Not performing unstripping as unity libs are not specified");
        }

        // Breaks .net runtime
        //using (new TimingCookie("Generating forwarded types"))
        //{
        //    Pass89GenerateForwarders.DoPass(rewriteContext);
        //}

        using (new TimingCookie("Writing xref cache"))
        {
            Pass89GenerateMethodXrefCache.DoPass(rewriteContext, options);
        }

        using (new TimingCookie("Writing assemblies"))
        {
            Pass90WriteToDisk.DoPass(rewriteContext, options);
        }

        using (new TimingCookie("Writing method pointer map"))
        {
            Pass91GenerateMethodPointerMap.DoPass(rewriteContext, options);
        }

        using (new TimingCookie("Clearing static data"))
        {
            Pass16ScanMethodRefs.MapOfCallers = new Dictionary<long, List<XrefInstance>>();
            Pass16ScanMethodRefs.NonDeadMethods = [];
        }

        Logger.Instance.LogInformation("Done!");

        rewriteContext.Dispose();
    }

    public void Dispose() { }

    /// <summary>
    /// Filters out assemblies that already have interop generated in the existing interop directory.
    /// </summary>
    private static List<AssemblyDefinition> FilterExistingAssemblies(
        List<AssemblyDefinition> source,
        string existingInteropDir)
    {
        var existingFiles = new HashSet<string>(
            Directory.EnumerateFiles(existingInteropDir, "*.dll")
                .Select(f => Path.GetFileNameWithoutExtension(f)),
            StringComparer.OrdinalIgnoreCase);

        return source
            .Where(asm => !existingFiles.Contains(asm.Name ?? string.Empty))
            .ToList();
    }
}
