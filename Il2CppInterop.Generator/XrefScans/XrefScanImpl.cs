using Il2CppInterop.Common.XrefScans;

namespace Il2CppInterop.Generator.XrefScans;

internal class XrefScanImpl : IXrefScannerImpl
{
    public (XrefScanUtil.InitMetadataForMethod, IntPtr)? GetMetadataResolver()
    {
        return null;
    }

    public bool XrefGlobalClassFilter(IntPtr movTarget)
    {
        return false;
    }
}
