using System.Text;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Common.Maps;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass89GenerateMethodXrefCache
{
    public static void DoPass(RewriteGlobalContext context, GeneratorOptions options)
    {
        var data = new List<MethodXrefScanCache.MethodData>();
        var existingAttributesPerAddress = new Dictionary<long, CachedScanResultsAttribute>();

        if (options.NoXrefCache)
            goto skipDataGather;

        foreach (var assemblyRewriteContext in context.Assemblies)
        {
            if (options.AdditionalAssembliesBlacklist.Contains(assemblyRewriteContext.NewAssembly.Name!))
                continue;

            var imports = assemblyRewriteContext.Imports;

            foreach (var typeRewriteContext in assemblyRewriteContext.Types)
                foreach (var methodRewriteContext in typeRewriteContext.Methods)
                {
                    var address = methodRewriteContext.Rva;

                    if (existingAttributesPerAddress.TryGetValue(address, out var attribute))
                    {
                        methodRewriteContext.NewMethod.CustomAttributes.Add(
                            new CustomAttribute((ICustomAttributeType?)imports.CachedScanResultsAttributector.Value, new CustomAttributeSignature([],
                            [
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.RefRangeStart),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), attribute.RefRangeStart)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.RefRangeEnd),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), attribute.RefRangeEnd)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.XrefRangeStart),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), attribute.RefRangeStart)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.XrefRangeEnd),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), attribute.RefRangeEnd)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.MetadataInitTokenRva),
                                    imports.Module.Long(),
                                    new CustomAttributeArgument(imports.Module.Long(), attribute.MetadataInitTokenRva)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.MetadataInitFlagRva),
                                    imports.Module.Long(),
                                    new CustomAttributeArgument(imports.Module.Long(), attribute.MetadataInitFlagRva))
                                ])));
                        continue;
                    }

                    var xrefStart = data.Count;

                    foreach (var xrefScanResult in methodRewriteContext.XrefScanResults)
                        data.Add(MethodXrefScanCache.MethodData.FromXrefInstance(xrefScanResult));

                    var xrefEnd = data.Count;

                    var refStart = 0;
                    var refEnd = 0;

                    if (address != 0)
                        if (Pass16ScanMethodRefs.MapOfCallers.TryGetValue(address, out var callerMap))
                        {
                            refStart = data.Count;
                            foreach (var xrefInstance in callerMap)
                                data.Add(MethodXrefScanCache.MethodData.FromXrefInstance(xrefInstance));

                            refEnd = data.Count;
                        }

                    if (xrefEnd != xrefStart || refStart != refEnd)
                    {
                        methodRewriteContext.NewMethod.CustomAttributes.Add(
                            new CustomAttribute((ICustomAttributeType?)imports.CachedScanResultsAttributector.Value, new CustomAttributeSignature([],
                            [
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.RefRangeStart),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), refStart)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.RefRangeEnd),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), refEnd)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.XrefRangeStart),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), xrefStart)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.XrefRangeEnd),
                                    imports.Module.Int(),
                                    new CustomAttributeArgument(imports.Module.Int(), xrefEnd)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.MetadataInitTokenRva),
                                    imports.Module.Long(),
                                    new CustomAttributeArgument(imports.Module.Long(), methodRewriteContext.MetadataInitTokenRva)),
                                new CustomAttributeNamedArgument(
                                    CustomAttributeArgumentMemberType.Field,
                                    nameof(CachedScanResultsAttribute.MetadataInitFlagRva),
                                    imports.Module.Long(),
                                    new CustomAttributeArgument(imports.Module.Long(), methodRewriteContext.MetadataInitFlagRva))
                                ])));

                        existingAttributesPerAddress[address] = new CachedScanResultsAttribute
                        {
                            RefRangeStart = refStart,
                            RefRangeEnd = refEnd,
                            XrefRangeStart = xrefStart,
                            XrefRangeEnd = xrefEnd,
                            MetadataInitFlagRva = methodRewriteContext.MetadataInitFlagRva,
                            MetadataInitTokenRva = methodRewriteContext.MetadataInitTokenRva
                        };
                    }
                }
        }

    skipDataGather:

        var header = new MethodXrefScanCache.FileHeader
        {
            Magic = MethodXrefScanCache.Magic,
            Version = MethodXrefScanCache.Version,
            InitMethodMetadataRva = XrefScanMetadataGenerationUtil.MetadataInitForMethodRva
        };

        using var writer =
            new BinaryWriter(
                new FileStream(Path.Combine(options.OutputDir, MethodXrefScanCache.FileName), FileMode.Create,
                    FileAccess.Write), Encoding.UTF8, false);
        writer.Write(header);

        foreach (var valueTuple in data)
            writer.Write(valueTuple);

        if (options.Verbose)
        {
            using var plainTextWriter =
                new StreamWriter(Path.Combine(options.OutputDir, MethodXrefScanCache.FileName + ".txt"));
            for (var i = 0; i < data.Count; i++)
                plainTextWriter.WriteLine($"{i}\t{data[i].Type}\t{data[i].Address}\t{data[i].FoundAt}");
        }
    }
}
