using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Common.Maps;

namespace Il2CppInterop.Common.XrefScans;

public static class XrefScanMethodDb
{
    private static readonly MethodAddressToTokenMap MethodMap;
    private static readonly MethodXrefScanCache XrefScanCache;
    private static readonly long GameAssemblyBase;

    private static XrefScanUtil.InitMetadataForMethod ourMetadataInitForMethodDelegate;

    static XrefScanMethodDb()
    {
        MethodMap = new MethodAddressToTokenMap(
            GeneratedDatabasesUtil.GetDatabasePath(MethodAddressToTokenMap.FileName));
        XrefScanCache = new MethodXrefScanCache(GeneratedDatabasesUtil.GetDatabasePath(MethodXrefScanCache.FileName));

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var errorPtr = IntPtr.Zero;
            var libHandle = dlopen("GameAssembly.dylib", 2);

            if (libHandle == IntPtr.Zero
                && Process.GetCurrentProcess().MainModule?.FileName is { } procPath
                && Directory.GetParent(procPath)?.Parent?.FullName is { } appContentsPath
                && Path.GetFileName(appContentsPath) == "Contents")
            {
                var gameAssemblyPath = Path.Combine(appContentsPath, "Frameworks", "GameAssembly.dylib");

                if (File.Exists(gameAssemblyPath))
                {
                    libHandle = dlopen(gameAssemblyPath, 2);
                }
            }

            if (libHandle == IntPtr.Zero)
            {
                errorPtr = dlerror();

                var errorMessage = errorPtr != IntPtr.Zero
                    ? Marshal.PtrToStringAnsi(errorPtr)
                    : "Unknown dlopen failure";

                throw new DllNotFoundException(
                    $"Failed to load \"GameAssembly.dylib\" with error message: {errorMessage}");
            }

            // Clear any previous error state.
            dlerror();

            var symbolAddress = dlsym(libHandle, "il2cpp_init");
            errorPtr = dlerror();

            if (errorPtr != IntPtr.Zero)
            {
                var errorMessage = Marshal.PtrToStringAnsi(errorPtr);

                throw new EntryPointNotFoundException(
                    $"Failed to find symbol \"il2cpp_init\" with error message: {errorMessage}");
            }

            if (dladdr(symbolAddress, out var info) == 0)
            {
                throw new InvalidOperationException();
            }

            var baseAddress = info.dli_fbase;
            GameAssemblyBase = (long)baseAddress;
        }
        else
        {
            GameAssemblyBase = (long)Process.GetCurrentProcess()
                .Modules.OfType<ProcessModule>()
                .Single(x => x.ModuleName is "GameAssembly.dll" or "GameAssembly.so" or "UserAssembly.dll" || string.Equals(x.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                .BaseAddress;
        }
    }

    public static MethodBase TryResolvePointer(IntPtr methodStart)
    {
        return MethodMap.Lookup((long)methodStart - GameAssemblyBase);
    }

    internal static IEnumerable<XrefInstance> ListUsers(CachedScanResultsAttribute attribute)
    {
        for (var i = attribute.RefRangeStart; i < attribute.RefRangeEnd; i++)
            yield return XrefScanCache.GetAt(i).AsXrefInstance(GameAssemblyBase);
    }

    internal static IEnumerable<XrefInstance> CachedXrefScan(CachedScanResultsAttribute attribute)
    {
        for (var i = attribute.XrefRangeStart; i < attribute.XrefRangeEnd; i++)
            yield return XrefScanCache.GetAt(i).AsXrefInstance(GameAssemblyBase);
    }

    internal static void CallMetadataInitForMethod(CachedScanResultsAttribute attribute)
    {
        if (attribute.MetadataInitFlagRva == 0 || attribute.MetadataInitTokenRva == 0)
            return;

        if (Marshal.ReadByte((IntPtr)(GameAssemblyBase + attribute.MetadataInitFlagRva)) != 0)
            return;

        if (ourMetadataInitForMethodDelegate == null)
            ourMetadataInitForMethodDelegate =
                Marshal.GetDelegateForFunctionPointer<XrefScanUtil.InitMetadataForMethod>(
                    (IntPtr)(GameAssemblyBase + XrefScanCache.Header.InitMethodMetadataRva));

        var token = Marshal.ReadInt32((IntPtr)(GameAssemblyBase + attribute.MetadataInitTokenRva));

        ourMetadataInitForMethodDelegate(token);

        Marshal.WriteByte((IntPtr)(GameAssemblyBase + attribute.MetadataInitFlagRva), 1);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DlInfo
    {
        public IntPtr dli_fname;
        public IntPtr dli_fbase;
        public IntPtr dli_sname;
        public IntPtr dli_saddr;
    }

    [DllImport("libSystem.dylib", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
    private static extern IntPtr dlopen(string filename, int flags);

    [DllImport("libSystem.dylib", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr dlerror();

    [DllImport("libSystem.dylib", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl,
        CharSet = CharSet.Ansi)]
    private static extern IntPtr dlsym(IntPtr handle, string symbol);

    [DllImport("libSystem.dylib", EntryPoint = "dladdr", CallingConvention = CallingConvention.Cdecl)]
    private static extern int dladdr(IntPtr addr, out DlInfo info);
}
