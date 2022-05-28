using System.Reflection;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Common.XrefScans;

internal static class XrefScanUtil
{
    private static InitMetadataForMethod ourMetadataInitForMethodDelegate;
    private static IntPtr ourMetadataInitForMethodPointer;

    internal static event Func<(InitMetadataForMethod, IntPtr)> InitRuntimeUtils;

    internal static unsafe bool CallMetadataInitForMethod(MethodBase method)
    {
        if (ourMetadataInitForMethodPointer == IntPtr.Zero)
        {
            var res = XrefScannerManager.Impl.GetMetadataResolver();
            if (res is ({ } m, var p))
            {
                ourMetadataInitForMethodDelegate = m;
                ourMetadataInitForMethodPointer = p;
            }
            else
            {
                return false;
            }
        }

        var nativeMethodInfoObject =
            Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method)?.GetValue(null);
        if (nativeMethodInfoObject == null) return false;
        var nativeMethodInfo = (IntPtr)nativeMethodInfoObject;
        var codeStart = *(IntPtr*)nativeMethodInfo;
        var firstCall = XrefScannerLowLevel.JumpTargets(codeStart).FirstOrDefault();
        if (firstCall != ourMetadataInitForMethodPointer || firstCall == IntPtr.Zero) return false;

        var tokenPointer =
            XrefScanUtilFinder.FindLastRcxReadAddressBeforeCallTo(codeStart, ourMetadataInitForMethodPointer);
        var initFlagPointer =
            XrefScanUtilFinder.FindByteWriteTargetRightAfterCallTo(codeStart, ourMetadataInitForMethodPointer);

        if (tokenPointer == IntPtr.Zero || initFlagPointer == IntPtr.Zero) return false;

        if (Marshal.ReadByte(initFlagPointer) == 0)
        {
            ourMetadataInitForMethodDelegate(Marshal.ReadInt32(tokenPointer));
            Marshal.WriteByte(initFlagPointer, 1);
        }

        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void InitMetadataForMethod(int metadataUsageToken);
}
