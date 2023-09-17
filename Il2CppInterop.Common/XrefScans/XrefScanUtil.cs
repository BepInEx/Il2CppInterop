using System.Reflection;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Common.XrefScans;

internal static class XrefScanUtil
{
    private static InitMetadataForMethodToken ourMetadataInitForMethodTokenDelegate;
    private static InitMetadataForMethodPointer ourMetadataInitForMethodPointerDelegate;
    private static IntPtr ourMetadataInitForMethodPointer;

    internal static unsafe bool CallMetadataInitForMethod(MethodBase method)
    {
        if (ourMetadataInitForMethodPointer == IntPtr.Zero)
        {
            var res = XrefScannerManager.Impl.GetMetadataResolver();
            if (res is IntPtr p)
            {
                if (false)
                {
                    ourMetadataInitForMethodTokenDelegate =
                        Marshal.GetDelegateForFunctionPointer<InitMetadataForMethodToken>(p);
                }
                else
                {
                    ourMetadataInitForMethodPointerDelegate =
                        Marshal.GetDelegateForFunctionPointer<InitMetadataForMethodPointer>(p);
                }
                // ourMetadataInitForMethodPointerDelegate = m;
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
        // var firstCall = XrefScannerLowLevel.JumpTargets(codeStart).FirstOrDefault();
        if (!XrefScannerLowLevel.JumpTargets(codeStart).Any(call => call == ourMetadataInitForMethodPointer)) return false;

        var initFlagPointer =
            XrefScanUtilFinder.FindByteWriteTargetRightAfterCallTo(codeStart, ourMetadataInitForMethodPointer);

        // var (initStart, initEnd) = XrefScannerLowLevel.ConditionalBlock(codeStart);

        // if (initStart == IntPtr.Zero || initEnd == IntPtr.Zero) return false;

        // var tokenPointer =
        //     XrefScanUtilFinder.FindLastRcxReadAddressesBeforeCallTo(initStart, (int)((ulong)initEnd - (ulong)initStart), ourMetadataInitForMethodPointer);

        var tokenPointer =
            XrefScanUtilFinder.FindLastRcxReadAddressesBeforeCallTo(codeStart, ourMetadataInitForMethodPointer);

        if (!tokenPointer.Any() || initFlagPointer == IntPtr.Zero) return false;

        if (Marshal.ReadByte(initFlagPointer) == 0)
        {
            foreach (var token in tokenPointer) {
                if (false)
                {
                    ourMetadataInitForMethodTokenDelegate(Marshal.ReadInt32(token));
                }
                else
                {
                    ourMetadataInitForMethodPointerDelegate(token);
                }
            }
            Marshal.WriteByte(initFlagPointer, 1);
        }

        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void InitMetadataForMethodToken(int metadataUsageToken);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void InitMetadataForMethodPointer(IntPtr metadataUsageToken);
}
