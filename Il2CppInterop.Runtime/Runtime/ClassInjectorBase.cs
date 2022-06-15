using System;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.Runtime;

internal struct InjectedClassData
{
    public IntPtr managedGcHandle;
}

public static class ClassInjectorBase
{
    public static object GetMonoObjectFromIl2CppPointer(IntPtr pointer)
    {
        var gcHandle = GetGcHandlePtrFromIl2CppObject(pointer);
        return GCHandle.FromIntPtr(gcHandle).Target;
    }

    public static unsafe IntPtr GetGcHandlePtrFromIl2CppObject(IntPtr pointer)
    {
        return GetInjectedData(pointer)->managedGcHandle;
    }

    internal static unsafe InjectedClassData* GetInjectedData(IntPtr objectPointer)
    {
        var pObjectClass = (Il2CppClass*)IL2CPP.il2cpp_object_get_class(objectPointer);
        return (InjectedClassData*)(objectPointer + (int)UnityVersionHandler.Wrap(pObjectClass).InstanceSize -
                                     sizeof(InjectedClassData)).ToPointer();
    }
}
