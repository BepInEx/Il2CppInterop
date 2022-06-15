using Il2CppInterop.Common.Host;

namespace Il2CppInterop.Common.XrefScans;

internal static class XrefScannerManagerExtensions
{
    public static T AddXrefScanner<T, TScanner>(this T host) where T : BaseHost where TScanner : IXrefScannerImpl, new()
    {
        host.AddComponent(new XrefScannerManager(new TScanner()));
        return host;
    }
}

internal class XrefScannerManager : IHostComponent
{
    private static IXrefScannerImpl s_xrefScanner;

    public static IXrefScannerImpl Impl
    {
        get
        {
            if (s_xrefScanner == null)
            {
                throw new InvalidOperationException("XrefScanner is not initialized! Initialize the host before using XrefScanner!");
            }

            return s_xrefScanner;
        }
    }

    public XrefScannerManager(IXrefScannerImpl impl)
    {
        s_xrefScanner = impl;
    }

    public void Dispose()
    {
        s_xrefScanner = null;
    }

    public void Start() { }
}
