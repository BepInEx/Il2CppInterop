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

        if (gcHandle == IntPtr.Zero) // The Garbage collector handle might return a null pointer
        {
            gcHandle = FallbackGetGcHandlePtrFromIl2CppDelegateMTarget(pointer);
        }

        return GCHandle.FromIntPtr(gcHandle).Target;
    }

    /// <summary>
    /// Tries to get the Garbage collector pointer from the m_target object from the m_target of the delegate.
    /// Fixes Harmony in Unity 2021.2.x .
    /// </summary>
    private static IntPtr FallbackGetGcHandlePtrFromIl2CppDelegateMTarget(IntPtr pointer)
    {
        if (IL2CPP.il2cpp_class_is_assignable_from(Il2CppClassPointerStore<Il2CppSystem.MulticastDelegate>.NativeClassPtr, IL2CPP.il2cpp_object_get_class(pointer)))
        {
            var delegateObject = new Il2CppSystem.Delegate(pointer);
            if (delegateObject.m_target != null && delegateObject.m_target.Pointer != IntPtr.Zero)
                return GetGcHandlePtrFromIl2CppObject(delegateObject.m_target.Pointer);
        }
        return IntPtr.Zero;
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
