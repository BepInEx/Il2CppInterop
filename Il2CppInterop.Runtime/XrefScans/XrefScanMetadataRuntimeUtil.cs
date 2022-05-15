using Il2CppInterop.Runtime.InteropTypes.Arrays;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
#if !MINI
using AppDomain = Il2CppSystem.AppDomain;
#endif

namespace Il2CppInterop.Runtime.XrefScans
{
    internal static class XrefScanMetadataRuntimeUtil
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void InitMetadataForMethod(int metadataUsageToken);

        private static InitMetadataForMethod ourMetadataInitForMethodDelegate;
        private static IntPtr ourMetadataInitForMethodPointer;
#if !MINI
        private static Func<AppDomain, Il2CppReferenceArray<Il2CppSystem.Reflection.Assembly>>? getAssemblies;
#endif
        private static unsafe void FindMetadataInitForMethod()
        {
#if !MINI
            getAssemblies ??=
                typeof(AppDomain).GetMethod("GetAssemblies", BindingFlags.Public | BindingFlags.Static)
                        ?.CreateDelegate(typeof(Func<AppDomain, Il2CppReferenceArray<Il2CppSystem.Reflection.Assembly>>)) as
                    Func<AppDomain, Il2CppReferenceArray<Il2CppSystem.Reflection.Assembly>>;

            var unityObjectCctor = getAssemblies(AppDomain.CurrentDomain)
                .Single(it => it.GetName().Name == "UnityEngine.CoreModule").GetType("UnityEngine.Object")
                .GetConstructors(Il2CppSystem.Reflection.BindingFlags.Static | Il2CppSystem.Reflection.BindingFlags.NonPublic).Single();
            var nativeMethodInfo = IL2CPP.il2cpp_method_get_from_reflection(unityObjectCctor.Pointer);
            ourMetadataInitForMethodPointer = XrefScannerLowLevel.JumpTargets(*(IntPtr*)nativeMethodInfo).First();
            ourMetadataInitForMethodDelegate = Marshal.GetDelegateForFunctionPointer<InitMetadataForMethod>(ourMetadataInitForMethodPointer);
#endif
        }

        internal static unsafe bool CallMetadataInitForMethod(MethodBase method)
        {
            if (ourMetadataInitForMethodPointer == IntPtr.Zero)
                FindMetadataInitForMethod();

            var nativeMethodInfoObject = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(method)?.GetValue(null);
            if (nativeMethodInfoObject == null) return false;
            var nativeMethodInfo = (IntPtr)nativeMethodInfoObject;
            var codeStart = *(IntPtr*)nativeMethodInfo;
            var firstCall = XrefScannerLowLevel.JumpTargets(codeStart).FirstOrDefault();
            if (firstCall != ourMetadataInitForMethodPointer || firstCall == IntPtr.Zero) return false;

            var tokenPointer = XrefScanUtilFinder.FindLastRcxReadAddressBeforeCallTo(codeStart, ourMetadataInitForMethodPointer);
            var initFlagPointer = XrefScanUtilFinder.FindByteWriteTargetRightAfterCallTo(codeStart, ourMetadataInitForMethodPointer);

            if (tokenPointer == IntPtr.Zero || initFlagPointer == IntPtr.Zero) return false;

            if (Marshal.ReadByte(initFlagPointer) == 0)
            {
                ourMetadataInitForMethodDelegate(Marshal.ReadInt32(tokenPointer));
                Marshal.WriteByte(initFlagPointer, 1);
            }

            return true;
        }
    }
}