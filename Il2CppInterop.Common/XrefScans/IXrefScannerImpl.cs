namespace Il2CppInterop.Common.XrefScans;

internal interface IXrefScannerImpl
{
    IntPtr? GetMetadataResolver();

    bool XrefGlobalClassFilter(IntPtr movTarget);
}
