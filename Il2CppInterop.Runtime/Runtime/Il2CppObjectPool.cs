using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes;
using Object = Il2CppSystem.Object;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    internal static bool DisableCaching { get; set; }

    private static readonly ConcurrentDictionary<IntPtr, WeakReference<Il2CppObjectBase>> s_cache = new();

    internal static void Remove(IntPtr ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    public static T Get<T>(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero) return default;

        var ownClass = IL2CPP.il2cpp_object_get_class(ptr);
        if (RuntimeSpecificsStore.IsInjected(ownClass))
        {
            var monoObject = ClassInjectorBase.GetMonoObjectFromIl2CppPointer(ptr);
            if (monoObject is T monoObjectT) return monoObjectT;
        }

        if (DisableCaching) return Il2CppObjectBase.InitializerStore<T>.Initializer(ptr);

        if (s_cache.TryGetValue(ptr, out var reference) && reference.TryGetTarget(out var cachedObject))
        {
            if (cachedObject is T cachedObjectT) return cachedObjectT;
            cachedObject.pooledPtr = IntPtr.Zero;
            // This leaves the case when you cast to an interface handled as if nothing was cached
        }

        var newObj = Il2CppObjectBase.InitializerStore<T>.Initializer(ptr);
        unsafe
        {
            var nativeClassStruct = UnityVersionHandler.Wrap((Il2CppClass*)Il2CppClassPointerStore<T>.NativeClassPtr);
            if (!nativeClassStruct.HasFinalize)
            {
                Il2CppSystem.GC.ReRegisterForFinalize(newObj as Object ?? new Object(ptr));
            }
        }

        var il2CppObjectBase = Unsafe.As<T, Il2CppObjectBase>(ref newObj);
        s_cache[ptr] = new WeakReference<Il2CppObjectBase>(il2CppObjectBase);
        il2CppObjectBase.pooledPtr = ptr;
        return newObj;
    }
}
