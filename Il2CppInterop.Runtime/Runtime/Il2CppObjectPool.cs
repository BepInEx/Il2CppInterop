using System;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    public static void HandleIl2CppFinalize(IntPtr ptr)
    {
    }

    public static T Get<T>(IntPtr ptr) where T : Il2CppObjectBase
    {
        return (T) Il2CppObjectBase.CreateUnsafe<T>(ptr);
    }
}
