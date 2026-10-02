using System.Runtime.InteropServices;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Common.XrefScans;

var arch = RuntimeInformation.ProcessArchitecture;
Console.WriteLine($"Process architecture: {arch}");
var failures = 0;
void Test(string name, Action test)
{
    try { test(); Console.WriteLine("PASS: " + name); }
    catch (Exception e) { failures++; Console.WriteLine($"FAIL: {name}: {e.GetType().Name}: {e.Message}"); }
}
void Unsupported(Action action)
{
    try { action(); }
    catch (PlatformNotSupportedException e) when (e.Message.Contains("x86/x64")) { return; }
    throw new Exception("Expected explicit unsupported-architecture error");
}
if (arch is not (Architecture.X86 or Architecture.X64))
{
    // Zero pointers deliberately avoid reading invalid native memory in the negative control.
    Test("JumpTargets refuses unsupported architecture before pointer access", () =>
        Unsupported(() => XrefScannerLowLevel.JumpTargets(IntPtr.Zero).ToArray()));
    Test("CallAndIndirectTargets refuses unsupported architecture before pointer access", () =>
        Unsupported(() => XrefScannerLowLevel.CallAndIndirectTargets(IntPtr.Zero).ToArray()));
    Test("uncached managed method refuses native metadata initialization", () =>
        Unsupported(() => XrefScanner.XrefScan(typeof(Fixture).GetMethod(nameof(Fixture.Uncached))!).ToArray()));
}
else
{
    // The bytes are read, never executed. x86 and x64 both encode CALL rel32; RET this way.
    var code = Marshal.AllocHGlobal(1024 * 1024);
    try
    {
        Marshal.Copy(new byte[] { 0xe8, 0x03, 0, 0, 0, 0xc3, 0, 0, 0xc3 }, 0, code, 9);
        Test("x86/x64 direct call scan remains available", () => {
            var targets = XrefScannerLowLevel.JumpTargets(code).ToArray();
            if (targets.Length != 1 || targets[0] != code + 8) throw new Exception("Wrong direct target");
        });
        Test("x86/x64 call-and-indirect scan remains available", () => {
            var targets = XrefScannerLowLevel.CallAndIndirectTargets(code).ToArray();
            if (targets.Length != 1 || targets[0] != code + 8) throw new Exception("Wrong direct target");
        });
    }
    finally { Marshal.FreeHGlobal(code); }
}
Test("empty cached results remain usable without native decoding", () => {
    if (XrefScanner.XrefScan(typeof(Fixture).GetMethod(nameof(Fixture.Cached))!).Any())
        throw new Exception("Expected empty cache");
});
Test("ordinary managed method still returns no native references", () => {
    if (XrefScanner.XrefScan(typeof(Fixture).GetMethod(nameof(Fixture.Managed))!).Any())
        throw new Exception("Expected no native method");
});
return failures == 0 ? 0 : 1;

public static class Fixture
{
    public static IntPtr NativeMethodInfoPtr_Test = IntPtr.Zero;
    public static IntPtr Uncached() => NativeMethodInfoPtr_Test;
    [CachedScanResults]
    public static IntPtr Cached() => NativeMethodInfoPtr_Test;
    public static int Managed() => 42;
}
