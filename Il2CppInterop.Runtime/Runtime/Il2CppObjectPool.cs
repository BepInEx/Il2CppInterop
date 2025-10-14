using System;
using System.Collections.Concurrent;
using Object = Il2CppSystem.Object;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    internal static bool DisableCaching { get; set; }

    private static readonly ConcurrentDictionary<IntPtr, WeakReference<Il2CppObjectBase>> s_cache = new();

    private static readonly ConcurrentDictionary<IntPtr, Func<IntPtr, object>> s_initializers = new();

    internal static void Remove(IntPtr ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    public static object? Get(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;

        if (s_cache.TryGetValue(ptr, out var reference) && reference.TryGetTarget(out var cachedObject))
        {
            return cachedObject;
        }

        var ownClass = IL2CPP.il2cpp_object_get_class(ptr);
        if (RuntimeSpecificsStore.IsInjected(ownClass))
        {
            return ClassInjectorBase.GetMonoObjectFromIl2CppPointer(ptr);
        }

        if (!s_initializers.TryGetValue(ownClass, out var initializer))
        {
            var className = IL2CPP.Il2CppStringToManaged(IL2CPP.il2cpp_class_get_name(ownClass));
            throw new InvalidOperationException($"No initializer found for class {className}");
        }

        var newObj = initializer(ptr);
        if (newObj is Il2CppObjectBase il2CppObjectBase)
        {
            s_cache[ptr] = new WeakReference<Il2CppObjectBase>(il2CppObjectBase);
            il2CppObjectBase.pooledPtr = ptr;
        }

        return newObj;
    }
}
