using System;
using System.Collections.Concurrent;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Runtime;

public static class Il2CppObjectPool
{
    private static readonly ConcurrentDictionary<IntPtr, WeakReference<Il2CppObjectBase>> s_cache = new();

    // Invoked when Il2Cpp destroys the object
    internal static void Remove(IntPtr ptr)
    {
        s_cache.TryRemove(ptr, out var obj);
    }

    // Invoked by generated assemblies when they want to do new T(ptr);
    public static T Get<T>(IntPtr ptr) where T : Il2CppObjectBase
    {
        var ownClass = IL2CPP.il2cpp_object_get_class(ptr);
        if (RuntimeSpecificsStore.IsInjected(ownClass))
        {
            var monoObject = ClassInjectorBase.GetMonoObjectFromIl2CppPointer(ptr);
            if (monoObject is T monoObjectT) return monoObjectT;
        }

        if (s_cache.TryGetValue(ptr, out var reference) && reference.TryGetTarget(out var cachedObject))
        {
            if (cachedObject is T cachedObjectT) return cachedObjectT;
        }

        var newObj = Il2CppObjectBase.InitializerStore<T>.Initializer(ptr);

        s_cache[ptr] = new WeakReference<Il2CppObjectBase>(newObj);
        newObj.pooledPtr = ptr;

        return newObj;
    }
}
