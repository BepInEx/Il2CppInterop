using System;
using System.Collections.Concurrent;
using Il2CppInterop.Common;
using Il2CppSystem;
using Object = Il2CppSystem.Object;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    internal static bool DisableCaching { get; set; }

    private static readonly ConcurrentDictionary<nint, WeakReference<Il2CppObjectBase>> s_cache = new();

    private static readonly ConcurrentDictionary<nint, Func<ObjectPointer, IObject>> s_initializers = new();

    internal static void Remove(nint ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    public static IObject? Get(nint ptr)
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
            return ClassInjectorBase.GetMonoObjectFromIl2CppPointer(ptr) as IObject;
        }

        if (!s_initializers.TryGetValue(ownClass, out var initializer))
        {
            var className = IL2CPP.Il2CppStringToManaged(IL2CPP.il2cpp_class_get_name(ownClass));
            throw new InvalidOperationException($"No initializer found for class {className}");
        }

        var newObj = initializer((ObjectPointer)ptr);
        if (newObj is Il2CppObjectBase il2CppObjectBase)
        {
            s_cache[ptr] = new WeakReference<Il2CppObjectBase>(il2CppObjectBase);
            il2CppObjectBase.pooledPtr = ptr;
        }

        return newObj;
    }

    public static void RegisterInitializer(nint classPtr, Func<ObjectPointer, IObject> initializer)
    {
        s_initializers[classPtr] = initializer;
    }
}
