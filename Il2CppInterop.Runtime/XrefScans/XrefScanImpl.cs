using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Reflection;
using BindingFlags = System.Reflection.BindingFlags;

namespace Il2CppInterop.Runtime.XrefScans;

internal class XrefScanImpl : IXrefScannerImpl
{
    private static Func<AppDomain, Il2CppReferenceArray<Assembly>>? getAssemblies;

    public unsafe (XrefScanUtil.InitMetadataForMethod, IntPtr)? GetMetadataResolver()
    {
        getAssemblies ??=
            typeof(AppDomain).GetMethod("GetAssemblies", BindingFlags.Public | BindingFlags.Static)
                    ?.CreateDelegate(typeof(Func<AppDomain, Il2CppReferenceArray<Assembly>>)) as
                Func<AppDomain, Il2CppReferenceArray<Assembly>>;

        var unityObjectCctor = getAssemblies(AppDomain.CurrentDomain)
            .Single(it => it.GetName().Name == "UnityEngine.CoreModule").GetType("UnityEngine.Object")
            .GetConstructors(Il2CppSystem.Reflection.BindingFlags.Static |
                             Il2CppSystem.Reflection.BindingFlags.NonPublic).Single();
        var nativeMethodInfo = IL2CPP.il2cpp_method_get_from_reflection(unityObjectCctor.Pointer);
        var ourMetadataInitForMethodPointer = XrefScannerLowLevel.JumpTargets(*(IntPtr*)nativeMethodInfo).First();
        var ourMetadataInitForMethodDelegate =
            Marshal.GetDelegateForFunctionPointer<XrefScanUtil.InitMetadataForMethod>(ourMetadataInitForMethodPointer);
        return (ourMetadataInitForMethodDelegate, ourMetadataInitForMethodPointer);
    }

    public bool XrefGlobalClassFilter(IntPtr movTarget)
    {
        var valueAtMov = (IntPtr)Marshal.ReadInt64(movTarget);
        if (valueAtMov != IntPtr.Zero)
        {
            var targetClass = (IntPtr)Marshal.ReadInt64(valueAtMov);
            return targetClass == Il2CppClassPointerStore<string>.NativeClassPtr ||
                   targetClass == Il2CppClassPointerStore<Type>.NativeClassPtr;
        }

        return false;
    }
}
