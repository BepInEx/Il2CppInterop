namespace Il2CppInterop.Common.XrefScans;

internal interface IXrefScannerImpl
{
    (XrefScanUtil.InitMetadataForMethod, IntPtr)? GetMetadataResolver();

    bool XrefGlobalClassFilter(IntPtr movTarget);
}
