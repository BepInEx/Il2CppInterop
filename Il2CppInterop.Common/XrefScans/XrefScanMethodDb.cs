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

    private static XrefScanUtil.InitMetadataForMethodToken ourMetadataInitForMethodTokenDelegate;
    private static XrefScanUtil.InitMetadataForMethodPointer ourMetadataInitForMethodPointerDelegate;

    static XrefScanMethodDb()
    {
        MethodMap = new MethodAddressToTokenMap(
            GeneratedDatabasesUtil.GetDatabasePath(MethodAddressToTokenMap.FileName));
        XrefScanCache = new MethodXrefScanCache(GeneratedDatabasesUtil.GetDatabasePath(MethodXrefScanCache.FileName));

        foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            if (module.ModuleName == "GameAssembly.dll")
            {
                GameAssemblyBase = (long)module.BaseAddress;
                break;
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
        if (attribute.MetadataInitFlagRva == 0 || !(attribute.MetadataInitTokenRvas?.Any() ?? false))
            return;

        if (Marshal.ReadByte((IntPtr)(GameAssemblyBase + attribute.MetadataInitFlagRva)) != 0)
            return;

        if (ourMetadataInitForMethodTokenDelegate == null)
            ourMetadataInitForMethodTokenDelegate =
                Marshal.GetDelegateForFunctionPointer<XrefScanUtil.InitMetadataForMethodToken>(
                    (IntPtr)(GameAssemblyBase + XrefScanCache.Header.InitMethodMetadataRva));
        if (ourMetadataInitForMethodPointerDelegate == null)
            ourMetadataInitForMethodPointerDelegate =
                Marshal.GetDelegateForFunctionPointer<XrefScanUtil.InitMetadataForMethodPointer>(
                    (IntPtr)(GameAssemblyBase + XrefScanCache.Header.InitMethodMetadataRva));

        foreach (var tokenRva in attribute.MetadataInitTokenRvas)
        {
            var token = (IntPtr)(GameAssemblyBase + tokenRva);

            if (false)
            {
                ourMetadataInitForMethodTokenDelegate(Marshal.ReadInt32(token));
            }
            else
            {
                ourMetadataInitForMethodPointerDelegate(token);
            }
        }

        Marshal.WriteByte((IntPtr)(GameAssemblyBase + attribute.MetadataInitFlagRva), 1);
    }
}
