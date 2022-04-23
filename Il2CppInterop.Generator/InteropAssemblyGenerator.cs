using System;
using System.IO;
using System.Linq;
using Iced.Intel;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Passes;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator;

public static class InteropAssemblyGenerator
{
    public static void GenerateInteropAssemblies(GeneratorOptions options)
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

        if (string.IsNullOrEmpty(options.MscorlibPath) && string.IsNullOrEmpty(options.SystemLibrariesPath))
        {
            Console.WriteLine("No mscorlib or system libraries specified; use -h for help");
            return;
        }

        if (!Directory.Exists(options.OutputDir))
            Directory.CreateDirectory(options.OutputDir);

        RewriteGlobalContext rewriteContext;
        IIl2CppMetadataAccess gameAssemblies;
        IMetadataAccess systemAssemblies;
        IMetadataAccess unityAssemblies;

        using (new TimingCookie("Reading assemblies"))
        {
            gameAssemblies = new CecilMetadataAccess(options.Source);
        }

        using (new TimingCookie("Reading system assemblies"))
        {
            if (!string.IsNullOrEmpty(options.SystemLibrariesPath))
                systemAssemblies = new CecilMetadataAccess(Directory
                    .EnumerateFiles(options.SystemLibrariesPath, "*.dll")
                    .Where(it =>
                        Path.GetFileName(it).StartsWith("System.") || Path.GetFileName(it) == "mscorlib.dll" ||
                        Path.GetFileName(it) == "netstandard.dll"));
            else
                systemAssemblies = new CecilMetadataAccess(new[] {options.MscorlibPath});
        }

        if (!string.IsNullOrEmpty(options.UnityBaseLibsDir))
            using (new TimingCookie("Reading unity assemblies"))
            {
                unityAssemblies = new CecilMetadataAccess(Directory.EnumerateFiles(options.UnityBaseLibsDir, "*.dll"));
            }
        else
            unityAssemblies = NullMetadataAccess.Instance;

        using (new TimingCookie("Creating rewrite assemblies"))
        {
            rewriteContext = new RewriteGlobalContext(options, gameAssemblies, systemAssemblies, unityAssemblies);
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

        Logger.Info($"{Pass18FinalizeMethodContexts.TotalPotentiallyDeadMethods} total potentially dead methods");
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

        using (new TimingCookie("Creating type getters"))
        {
            Pass24GenerateTypeStaticGetters.DoPass(rewriteContext);
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
        else
        {
            Logger.Warning("Not performing unstripping as unity libs are not specified");
        }

        using (new TimingCookie("Generating forwarded types"))
        {
            Pass89GenerateForwarders.DoPass(rewriteContext);
        }

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

        if (!options.NoCopyRuntimeLibs)
        {
            File.Copy(typeof(IL2CPP).Assembly.Location,
                Path.Combine(options.OutputDir, typeof(IL2CPP).Assembly.GetName().Name + ".dll"), true);
            File.Copy(typeof(Decoder).Assembly.Location,
                Path.Combine(options.OutputDir, typeof(Decoder).Assembly.GetName().Name + ".dll"), true);
        }

        Logger.Info("Done!");

        rewriteContext.Dispose();
    }
}