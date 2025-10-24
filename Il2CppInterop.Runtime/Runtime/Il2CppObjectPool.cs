using System;
using System.Collections.Concurrent;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    internal static bool DisableCaching { get; set; }

    private static readonly ConcurrentDictionary<nint, WeakReference<Object>> s_cache = new();

    private static readonly ConcurrentDictionary<nint, Func<ObjectPointer, object>> s_initializers = new();

    public static void Remove(nint ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    public static object? Get(nint ptr)
    {
        if (ptr == nint.Zero)
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
            var className = IL2CPP.il2cpp_class_get_name_(ownClass);
            throw new InvalidOperationException($"No initializer found for class {className}");
        }

        var newObj = initializer((ObjectPointer)ptr);
        if (newObj is Object @object)
        {
            if (!DisableCaching)
            {
                s_cache[ptr] = new WeakReference<Object>(@object);
            }
        }

        return newObj;
    }

    public static void RegisterInitializer(nint classPtr, Func<ObjectPointer, object> initializer)
    {
        s_initializers[classPtr] = initializer;
    }

    public static unsafe object ValueTypeInitializer<T>(ObjectPointer obj) where T : struct, IIl2CppType<T>
    {
        var unboxed = IL2CPP.il2cpp_object_unbox((nint)obj);
        return Il2CppTypeHelper.ReadFromPointer<T>((void*)unboxed);
    }
}
