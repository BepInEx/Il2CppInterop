using System;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Injection;

public static class TypeInjector
{
    public static void RegisterTypeInIl2Cpp<T>() where T : IIl2CppType<T>
    {
        RegisterTypeInIl2Cpp(typeof(T));
    }

    private static void RegisterTypeInIl2Cpp(Type type)
    {
        var result = Il2CppClassPointerStore.GetNativeClassPointer(type);
        if (result is not 0)
        {
            // Already registered
            return;
        }
    }
}
