using Il2CppInterop.Runtime.XrefScans;

namespace Il2CppInterop.Runtime;

// TODO: Remove
internal static class Initializer
{
    // [ModuleInitializer]
    internal static void Initialize()
    {
        XrefScanImpl.Initialize();
    }
}