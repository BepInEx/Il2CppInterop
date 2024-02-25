using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.XrefScans;
using Il2CppMono.Xml;

namespace Il2CppInterop.Runtime.XrefScans;

internal class XrefScanImpl : IXrefScannerImpl
{
    public unsafe IntPtr? GetMetadataResolver()
    {
        var unityObjectCctor = Il2CppType.Of<SmallXmlParser>()
            .GetConstructors(Il2CppSystem.Reflection.BindingFlags.Instance |
                             Il2CppSystem.Reflection.BindingFlags.Public).Single();
        var nativeMethodInfo = IL2CPP.il2cpp_method_get_from_reflection(unityObjectCctor.Pointer);
        var ourMetadataInitForMethodPointer = XrefScannerLowLevel.JumpTargets(*(IntPtr*)nativeMethodInfo).First();
        return ourMetadataInitForMethodPointer;
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
