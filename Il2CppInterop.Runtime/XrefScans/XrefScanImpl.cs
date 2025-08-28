using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.InteropTypes.CoreLib;
using Il2CppSystem;
using Il2CppSystem.Reflection;

namespace Il2CppInterop.Runtime.XrefScans;

internal class XrefScanImpl : IXrefScannerImpl
{
    public unsafe (XrefScanUtil.InitMetadataForMethod, nint)? GetMetadataResolver()
    {
        var unityObjectCctor = AppDomainAccessor.GetAssembliesInCurrentDomain()
            .Single(it => it.GetName().Name == "UnityEngine.CoreModule").GetType("UnityEngine.Object")
            .GetConstructors(BindingFlags.Static | BindingFlags.NonPublic)
            .Single();
        var nativeMethodInfo = IL2CPP.il2cpp_method_get_from_reflection(unityObjectCctor.Pointer);
        var ourMetadataInitForMethodPointer = XrefScannerLowLevel.JumpTargets(*(nint*)nativeMethodInfo).First();
        var ourMetadataInitForMethodDelegate =
            Marshal.GetDelegateForFunctionPointer<XrefScanUtil.InitMetadataForMethod>(ourMetadataInitForMethodPointer);
        return (ourMetadataInitForMethodDelegate, ourMetadataInitForMethodPointer);
    }

    public bool XrefGlobalClassFilter(nint movTarget)
    {
        var valueAtMov = (nint)Marshal.ReadInt64(movTarget);
        if (valueAtMov != nint.Zero)
        {
            var targetClass = (nint)Marshal.ReadInt64(valueAtMov);
            return targetClass == Il2CppClassPointerStore<String>.NativeClassPtr ||
                   targetClass == Il2CppClassPointerStore<Type>.NativeClassPtr;
        }

        return false;
    }
}
