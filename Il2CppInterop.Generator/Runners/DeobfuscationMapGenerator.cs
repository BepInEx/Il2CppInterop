using System.IO.Compression;
using System.Text;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.MetadataAccess;
using Il2CppInterop.Generator.Passes;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Runners;

public static class DeobfuscationMapGenerator
{
    public static Il2CppInteropGenerator AddDeobfuscationMapGenerator(this Il2CppInteropGenerator gen)
    {
        return gen.AddRunner<DeobfuscationMapGeneratorRunner>();
    }
}

internal class DeobfuscationMapGeneratorRunner : IRunner
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

        if (string.IsNullOrEmpty(options.DeobfuscationNewAssembliesPath))
        {
            Console.WriteLine("No obfuscated assembly path specified; use -h for help");
            return;
        }

        if (!Directory.Exists(options.OutputDir))
            Directory.CreateDirectory(options.OutputDir);

        RewriteGlobalContext rewriteContext;
        IIl2CppMetadataAccess inputAssemblies;
        using (new TimingCookie("Reading assemblies"))
        {
            inputAssemblies =
                new AssemblyMetadataAccess(Directory.EnumerateFiles(options.DeobfuscationNewAssembliesPath, "*.dll"));
        }

        using (new TimingCookie("Creating rewrite assemblies"))
        {
            rewriteContext = new RewriteGlobalContext(options, inputAssemblies, NullMetadataAccess.Instance);
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


        RewriteGlobalContext cleanContext;
        IIl2CppMetadataAccess cleanAssemblies;
        using (new TimingCookie("Reading clean assemblies"))
        {
            cleanAssemblies = new AssemblyMetadataAccess(options.Source);
        }

        using (new TimingCookie("Creating clean rewrite assemblies"))
        {
            cleanContext = new RewriteGlobalContext(options, cleanAssemblies, NullMetadataAccess.Instance);
        }

        using (new TimingCookie("Computing clean assembly renames"))
        {
            Pass05CreateRenameGroups.DoPass(cleanContext);
        }

        using (new TimingCookie("Creating clean assembly typedefs"))
        {
            Pass10CreateTypedefs.DoPass(cleanContext);
        }


        var usedNames = new Dictionary<TypeDefinition, (string OldName, int Penalty, bool ForceNs)>();

        using var fileOutput = new FileStream(options.OutputDir + Path.DirectorySeparatorChar + "RenameMap.csv.gz",
            FileMode.Create, FileAccess.Write);
        using var gzipStream = new GZipStream(fileOutput, CompressionLevel.Optimal, true);
        using var writer = new StreamWriter(gzipStream, Encoding.UTF8, 65536, true);

        void DoEnum(TypeRewriteContext obfuscatedType, TypeRewriteContext cleanType)
        {
            foreach (var originalTypeField in obfuscatedType.OriginalType.Fields)
            {
                if (!originalTypeField.Name.IsObfuscated(obfuscatedType.AssemblyContext.GlobalContext.Options))
                    continue;
                var matchedField =
                    cleanType.OriginalType.Fields[obfuscatedType.OriginalType.Fields.IndexOf(originalTypeField)];

                writer.WriteLine(obfuscatedType.NewType.GetNamespacePrefix() + "." + obfuscatedType.NewType.Name +
                                 "::" + Pass22GenerateEnums.GetUnmangledName(originalTypeField) + ";" +
                                 matchedField.Name + ";0");
            }
        }

        foreach (var assemblyContext in rewriteContext.Assemblies)
        {
            if (options.DeobfuscationGenerationAssemblies.Count > 0 &&
                !options.DeobfuscationGenerationAssemblies.Contains(assemblyContext.NewAssembly.Name!))
                continue;

            var cleanAssembly = cleanContext.GetAssemblyByName(assemblyContext.OriginalAssembly.Name!);

            void DoType(TypeRewriteContext typeContext, TypeRewriteContext? enclosingType)
            {
                if (!typeContext.OriginalNameWasObfuscated) return;

                var cleanType = FindBestMatchType(typeContext, cleanAssembly, enclosingType);
                if (cleanType.Item1 == null) return;

                if (!usedNames.TryGetValue(cleanType.Item1.NewType, out var existing) ||
                    existing.Item2 < cleanType.Item2)
                    usedNames[cleanType.Item1.NewType] = (
                        typeContext.NewType.GetNamespacePrefix() + "." + typeContext.NewType.Name, cleanType.Item2,
                        typeContext.OriginalType.Namespace != cleanType.Item1.OriginalType.Namespace);
                else
                    return;

                if (typeContext.OriginalType.IsEnum)
                    DoEnum(typeContext, cleanType.Item1);

                foreach (var originalTypeNestedType in typeContext.OriginalType.NestedTypes)
                    DoType(typeContext.AssemblyContext.GetContextForOriginalType(originalTypeNestedType),
                        cleanType.Item1);
            }

            foreach (var typeContext in assemblyContext.Types)
            {
                if (typeContext.NewType.DeclaringType != null) continue;

                DoType(typeContext, null);
            }
        }


        foreach (var keyValuePair in usedNames)
            writer.WriteLine(keyValuePair.Value.Item1 + ";" +
                             (keyValuePair.Value.ForceNs ? keyValuePair.Key.Namespace + "." : "") +
                             keyValuePair.Key.Name + ";" + keyValuePair.Value.Item2);

        Logger.Instance.LogInformation("Done!");

        rewriteContext.Dispose();
    }

    private static (TypeRewriteContext?, int) FindBestMatchType(TypeRewriteContext obfType,
        AssemblyRewriteContext cleanAssembly, TypeRewriteContext? enclosingCleanType)
    {
        var inheritanceDepthOfOriginal = 0;
        var currentBase = obfType.OriginalType.BaseType;
        while (true)
        {
            if (currentBase == null) break;
            var currentBaseContext =
                obfType.AssemblyContext.GlobalContext.TryGetNewTypeForOriginal(currentBase.Resolve()!);
            if (currentBaseContext == null || !currentBaseContext.OriginalNameWasObfuscated) break;

            inheritanceDepthOfOriginal++;
            currentBase = currentBaseContext.OriginalType.BaseType;
        }

        var bestPenalty = int.MinValue;
        TypeRewriteContext? bestMatch = null;

        var source =
            enclosingCleanType?.OriginalType.NestedTypes.Select(it =>
                cleanAssembly.GlobalContext.GetNewTypeForOriginal(it)) ??
            cleanAssembly.Types.Where(it => it.NewType.DeclaringType == null);

        foreach (var candidateCleanType in source)
        {
            if (obfType.OriginalType.HasMethods() != candidateCleanType.OriginalType.HasMethods())
                continue;

            if (obfType.OriginalType.HasFields() != candidateCleanType.OriginalType.HasFields())
                continue;

            if (obfType.OriginalType.IsEnum)
                if (obfType.OriginalType.Fields.Count != candidateCleanType.OriginalType.Fields.Count)
                    continue;

            var currentPenalty = 0;

            var tryBase = candidateCleanType.OriginalType.BaseType;
            var actualBaseDepth = 0;
            while (tryBase != null)
            {
                if (tryBase?.Name == currentBase?.Name && tryBase?.Namespace == currentBase?.Namespace)
                    break;

                tryBase = tryBase?.Resolve()?.BaseType;
                actualBaseDepth++;
            }

            if (tryBase == null && currentBase != null)
                continue;

            var baseDepthDifference = Math.Abs(actualBaseDepth - inheritanceDepthOfOriginal);
            if (baseDepthDifference > 1) continue; // heuristic optimization
            currentPenalty -= baseDepthDifference * 50;

            currentPenalty -=
                Math.Abs(candidateCleanType.OriginalType.Fields.Count - obfType.OriginalType.Fields.Count) * 5;

            currentPenalty -= Math.Abs(obfType.OriginalType.NestedTypes.Count -
                                       candidateCleanType.OriginalType.NestedTypes.Count) * 10;

            currentPenalty -=
                Math.Abs(obfType.OriginalType.Properties.Count - candidateCleanType.OriginalType.Properties.Count) * 5;

            currentPenalty -=
                Math.Abs(obfType.OriginalType.Interfaces.Count - candidateCleanType.OriginalType.Interfaces.Count) * 35;

            var options = obfType.AssemblyContext.GlobalContext.Options;

            foreach (var obfuscatedField in obfType.OriginalType.Fields)
            {
                if (obfuscatedField.Name.IsObfuscated(options))
                {
                    var bestFieldScore = candidateCleanType.OriginalType.Fields.Max(it =>
                        TypeMatchWeight(obfuscatedField.Signature!.FieldType, it.Signature!.FieldType, options));
                    currentPenalty += bestFieldScore * (bestFieldScore < 0 ? 10 : 2);
                    continue;
                }

                if (candidateCleanType.OriginalType.Fields.Any(it => it.Name == obfuscatedField.Name))
                    currentPenalty += 10;
            }

            foreach (var obfuscatedMethod in obfType.OriginalType.Methods)
            {
                if (obfuscatedMethod.IsConstructor) continue;

                if (obfuscatedMethod.Name.IsObfuscated(options))
                {
                    var bestMethodScore = candidateCleanType.OriginalType.Methods.Max(it =>
                        MethodSignatureMatchWeight(obfuscatedMethod, it, options));
                    currentPenalty += bestMethodScore * (bestMethodScore < 0 ? 10 : 1);

                    continue;
                }

                if (candidateCleanType.OriginalType.Methods.Any(it => it.Name == obfuscatedMethod.Name))
                    currentPenalty += (obfuscatedMethod.Name?.Length ?? 0) / 10 * 5 + 1;
            }

            if (currentPenalty == bestPenalty)
            {
                bestMatch = null;
            }
            else if (currentPenalty > bestPenalty)
            {
                bestPenalty = currentPenalty;
                bestMatch = candidateCleanType;
            }
        }

        // if (bestPenalty < -100)
        // bestMatch = null;

        return (bestMatch, bestPenalty);
    }

    private static int TypeMatchWeight(TypeSignature a, TypeSignature b, GeneratorOptions options)
    {
        if (a.GetType() != b.GetType())
            return -1;

        var runningSum = 0;

        void Accumulate(int i)
        {
            if (i < 0 || runningSum < 0)
                runningSum = -1;
            else
                runningSum += i;
        }

        switch (a)
        {
            case ArrayBaseTypeSignature arr:
                if (b is not ArrayBaseTypeSignature brr)
                    return -1;
                return TypeMatchWeight(arr.BaseType, brr.BaseType, options) * 5;
            case ByReferenceTypeSignature abr:
                if (b is not ByReferenceTypeSignature bbr)
                    return -1;
                return TypeMatchWeight(abr.BaseType, bbr.BaseType, options) * 5;
            case GenericInstanceTypeSignature agi:
                if (b is not GenericInstanceTypeSignature bgi)
                    return -1;
                if (agi.TypeArguments.Count != bgi.TypeArguments.Count) return -1;
                Accumulate(TypeMatchWeight(agi.GenericType.ToTypeSignature(), bgi.GenericType.ToTypeSignature(), options));
                for (var i = 0; i < agi.TypeArguments.Count; i++)
                    Accumulate(TypeMatchWeight(agi.TypeArguments[i], bgi.TypeArguments[i], options));
                return runningSum * 5;
            case GenericParameterSignature:
                if (b is not GenericParameterSignature)
                    return -1;
                return 5;
            default:
                if (a.IsNested())
                {
                    if (!b.IsNested())
                        return -1;

                    if (a.Name.IsObfuscated(options))
                        return 0;

                    var declMatch = TypeMatchWeight(a.DeclaringType!.ToTypeSignature(), b.DeclaringType!.ToTypeSignature(), options);
                    if (declMatch == -1 || a.Name != b.Name)
                        return -1;

                    return 1;
                }

                if (a.Name.IsObfuscated(options))
                    return 0;
                return a.Name == b.Name && a.Namespace == b.Namespace ? 1 : -1;
        }
    }

    private static int MethodSignatureMatchWeight(MethodDefinition a, MethodDefinition b, GeneratorOptions options)
    {
        if (a.Parameters.Count != b.Parameters.Count || a.IsStatic != b.IsStatic ||
            (a.Attributes & MethodAttributes.MemberAccessMask) !=
            (b.Attributes & MethodAttributes.MemberAccessMask))
            return -1;

        var runningSum = TypeMatchWeight(a.Signature!.ReturnType, b.Signature!.ReturnType, options);
        if (runningSum == -1)
            return -1;

        void Accumulate(int i)
        {
            if (i < 0 || runningSum < 0)
                runningSum = -1;
            else
                runningSum += i;
        }

        for (var i = 0; i < a.Parameters.Count; i++)
            Accumulate(TypeMatchWeight(a.Parameters[i].ParameterType, b.Parameters[i].ParameterType, options));

        return runningSum * (a.Parameters.Count + 1);
    }

    public void Dispose() { }
}
