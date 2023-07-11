using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Text;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Il2CppInterop.Generator.XrefScans;

namespace Il2CppInterop.Generator.Passes;

public static class Pass17ScanMethodRefs
{
    public static readonly HashSet<long> NonDeadMethods = new();
    public static IDictionary<long, List<XrefInstance>> MapOfCallers = new Dictionary<long, List<XrefInstance>>();

    public static void DoPass(RewriteGlobalContext context, GeneratorOptions options)
    {
        if (string.IsNullOrEmpty(options.GameAssemblyPath))
        {
            Pass16GenerateMemberContexts.HasObfuscatedMethods = false;
            return;
        }

        using var mappedFile = MemoryMappedFile.CreateFromFile(options.GameAssemblyPath, FileMode.Open, null, 0,
            MemoryMappedFileAccess.Read);
        using var accessor = mappedFile.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);

        nint gameAssemblyPtr;

        unsafe
        {
            byte* fileStartPtr = null;
            accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref fileStartPtr);
            gameAssemblyPtr = (nint)fileStartPtr;
        }

        context.HasGcWbarrierFieldWrite =
            FindByteSequence(gameAssemblyPtr, accessor.Capacity, "il2cpp_gc_wbarrier_set_field");

        if (!Pass16GenerateMemberContexts.HasObfuscatedMethods) return;

        var methodToCallersMap = new ConcurrentDictionary<long, List<XrefInstance>>();
        var methodToCalleesMap = new ConcurrentDictionary<long, List<nint>>();

        context.MethodStartAddresses.Sort();

        // Scan xrefs
        context.Assemblies.SelectMany(it => it.Types).SelectMany(it => it.Methods).AsParallel().ForAll(
            originalTypeMethod =>
            {
                var address = originalTypeMethod.FileOffset;
                if (address == 0) return;

                if (!options.NoXrefCache)
                {
                    var pair = XrefScanMetadataGenerationUtil.FindMetadataInitForMethod(originalTypeMethod, gameAssemblyPtr);
                    originalTypeMethod.MetadataInitFlagRva = pair.FlagRva;
                    originalTypeMethod.MetadataInitTokenRva = pair.TokenRva;
                }

                var nextMethodStart = context.MethodStartAddresses.BinarySearch(address + 1);
                if (nextMethodStart < 0) nextMethodStart = ~nextMethodStart;
                var length = nextMethodStart >= context.MethodStartAddresses.Count
                    ? 1024 * 1024
                    : context.MethodStartAddresses[nextMethodStart] - address;
                foreach (var callTargetGlobal in XrefScanner.XrefScanImpl(
                             XrefScanner.DecoderForAddress(IntPtr.Add(gameAssemblyPtr, (int)address), (int)length),
                             true))
                {
                    var callTarget = callTargetGlobal.RelativeToBase(gameAssemblyPtr + (nint)originalTypeMethod.FileOffset - (nint)originalTypeMethod.Rva);
                    if (callTarget.Type == XrefType.Method)
                    {
                        var targetRelative = callTarget.Pointer;
                        methodToCallersMap.GetOrAdd(targetRelative, _ => new List<XrefInstance>()).AddLocked(
                            new XrefInstance(XrefType.Method, (nint)originalTypeMethod.Rva, callTarget.FoundAt));
                        methodToCalleesMap.GetOrAdd(originalTypeMethod.Rva, _ => new List<nint>())
                            .AddLocked(targetRelative);
                    }

                    if (!options.NoXrefCache)
                        originalTypeMethod.XrefScanResults.Add(callTarget);
                }
            });

        MapOfCallers = methodToCallersMap;

        void MarkMethodAlive(long address)
        {
            if (!NonDeadMethods.Add(address)) return;
            if (!methodToCalleesMap.TryGetValue(address, out var calleeList)) return;

            foreach (var callee in calleeList)
                MarkMethodAlive(callee);
        }

        // Now decided which of them are possible dead code
        foreach (var assemblyRewriteContext in context.Assemblies)
            foreach (var typeRewriteContext in assemblyRewriteContext.Types)
                foreach (var methodRewriteContext in typeRewriteContext.Methods)
                {
                    if (methodRewriteContext.FileOffset == 0) continue;

                    var originalMethod = methodRewriteContext.OriginalMethod;
                    if (!originalMethod.Name.IsObfuscated(options) || originalMethod.IsVirtual)
                        MarkMethodAlive(methodRewriteContext.Rva);
                }
    }

    private static unsafe bool FindByteSequence(nint basePtr, long length, string str)
    {
        var bytes = (byte*)basePtr;
        var sequence = Encoding.UTF8.GetBytes(str);
        for (var i = 0L; i < length; i++)
        {
            for (var j = 0; j < sequence.Length; j++)
                if (bytes[i + j] != sequence[j])
                    goto next;

            return true;

        next:;
        }

        return false;
    }
}
