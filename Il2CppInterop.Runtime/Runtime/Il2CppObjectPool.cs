using System;
using System.Collections.Concurrent;
using Il2CppInterop.Runtime.InteropTypes;
using Object = Il2CppSystem.Object;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    public static bool DisableCaching { get; set; }

    private static readonly ConcurrentDictionary<IntPtr, WeakReference<Il2CppObjectBase>> s_cache = new();

    internal static void Remove(IntPtr ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    internal static void Free(nint unmanagedGcHandle, IntPtr ptr)
    {
        if (IL2CPP.il2cpp_gchandle_get_target(unmanagedGcHandle) != IntPtr.Zero)
            IL2CPP.il2cpp_gchandle_free(unmanagedGcHandle);
        Remove(ptr);
    }

    public static void InternWeak(Il2CppObjectBase obj)
    {
        IntPtr ptr = obj.Pointer;
        obj.pooledPtr = ptr;
        s_cache[ptr] = new(obj);
    }

    public static T Get<T>(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero || Il2CppFinalizers.s_dying.ContainsKey(ptr))
        {
            return default!;
        }

        if (DisableCaching) return Il2CppObjectInitializer.New<T>(ptr);

        if (s_cache.TryGetValue(ptr, out var reference))
        {
            if (reference.TryGetTarget(out var cachedObject))
            {
                if (cachedObject is T cachedObjectT) return cachedObjectT;

                // This leaves the case when you cast to an interface, handled as if nothing was cached
            }

            // If the cached object no longer exists, delete the irrelevant entry if we can:
            Remove(ptr);
        }

        var newObj = Il2CppObjectInitializer.New<T>(ptr);
        unsafe
        {
            var nativeClassStruct = UnityVersionHandler.Wrap((Il2CppClass*)Il2CppClassPointerStore<T>.NativeClassPtr);
            if (!nativeClassStruct.HasFinalize)
            {
                Il2CppSystem.GC.ReRegisterForFinalize(newObj as Object ?? new Object(ptr));
            }
        }

        return newObj;
    }
}
