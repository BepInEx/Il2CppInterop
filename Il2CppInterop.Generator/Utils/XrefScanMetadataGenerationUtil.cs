using System;
using System.Linq;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Utils;

internal static class XrefScanMetadataGenerationUtil
{
    internal static long MetadataInitForMethodRva;
    internal static IntPtr MetadataInitForMethodFileOffset;

    private static readonly (string Assembly, string Type, string Method)[] MetadataInitCandidates =
    {
        ("UnityEngine.CoreModule", "UnityEngine.Object", ".cctor"),
        ("mscorlib", "System.Exception", "get_Message"),
        ("mscorlib", "System.IntPtr", "Equals")
    };

    private static void FindMetadataInitForMethod(RewriteGlobalContext context, long gameAssemblyBase)
    {
        foreach (var metadataInitCandidate in MetadataInitCandidates)
        {
            var assembly =
                context.Assemblies.FirstOrDefault(it =>
                    it.OriginalAssembly.Name.Name == metadataInitCandidate.Assembly);
            var unityObjectCctor = assembly?.TryGetTypeByName(metadataInitCandidate.Type)?.OriginalType.Methods
                .FirstOrDefault(it => it.Name == metadataInitCandidate.Method);

            if (unityObjectCctor == null) continue;

            MetadataInitForMethodFileOffset =
                (IntPtr)(long)XrefScannerLowLevel
                    .JumpTargets((IntPtr)(gameAssemblyBase + unityObjectCctor.ExtractOffset())).First();
            MetadataInitForMethodRva = (long)MetadataInitForMethodFileOffset - gameAssemblyBase -
                unityObjectCctor.ExtractOffset() + unityObjectCctor.ExtractRva();

            return;
        }

        throw new ApplicationException("Unable to find a method with metadata init reference");
    }

    internal static (long FlagRva, long[] TokenRvas) FindMetadataInitForMethod(MethodRewriteContext method,
        long gameAssemblyBase)
    {
        if (MetadataInitForMethodRva == 0)
            FindMetadataInitForMethod(method.DeclaringType.AssemblyContext.GlobalContext, gameAssemblyBase);

        var codeStart = (IntPtr)(gameAssemblyBase + method.FileOffset);
        if (!XrefScannerLowLevel.JumpTargets(codeStart).Any(call => call == MetadataInitForMethodFileOffset)) return (0, Array.Empty<long>());

        var initFlagPointer =
            XrefScanUtilFinder.FindByteWriteTargetRightAfterCallTo(codeStart, MetadataInitForMethodFileOffset);

        // var (initStart, initEnd) = XrefScannerLowLevel.ConditionalBlock(codeStart);

        // if (initStart == IntPtr.Zero || initEnd == IntPtr.Zero) return (0, Array.Empty<long>());

        // var tokenPointer =
        //     XrefScanUtilFinder.FindLastRcxReadAddressesBeforeCallTo(initStart, (int)((ulong)initEnd - (ulong)initStart), MetadataInitForMethodFileOffset);

        var tokenPointer =
            XrefScanUtilFinder.FindLastRcxReadAddressesBeforeCallTo(codeStart, MetadataInitForMethodFileOffset);

        if (!tokenPointer.Any() || initFlagPointer == IntPtr.Zero) return (0, Array.Empty<long>());

        return ((long)initFlagPointer - gameAssemblyBase - method.FileOffset + method.Rva,
            tokenPointer.Select(pointer => (long)pointer - gameAssemblyBase - method.FileOffset + method.Rva).ToArray());
    }
}
