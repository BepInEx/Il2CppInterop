using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
using Microsoft.Extensions.Logging;
using Object = Il2CppSystem.Object;

namespace Il2CppInterop.Runtime.Runtime;

// NB: Since we may call GC.SuppressFinalize on managed objects, we need another object (this one) to handle the cleanup.
// The objects' lifetimes are linked by the s_finalizers ephemeron on Il2CppFinalizers.
internal class FinalizerContainer
{
    public nint gcHandle;
    public IntPtr pooledPtr;

    ~FinalizerContainer() => Il2CppObjectPool.Free(gcHandle, pooledPtr);
}

internal static class Il2CppFinalizers
{
    // Dying objects are never duplicated.
    internal static readonly ConcurrentDictionary<IntPtr, byte> s_dying = new();
    internal static readonly ConditionalWeakTable<Il2CppObjectBase, FinalizerContainer> s_finalizers = new();

    internal static void OnDeath(IntPtr ptr)
    {
        s_dying.Remove(ptr, out _);
    }

    internal static bool ShouldFinalize(IntPtr ptr)
    {
        return !s_dying.ContainsKey(ptr);
    }

    internal static void Finalize(Il2CppObjectBase obj)
    {
        obj.finalizerState = Il2CppObjectFinalizerState.Now;
        s_dying[obj.Pointer] = 0;
        GC.ReRegisterForFinalize(obj);
    }

    internal static void OverrideFinalize(Il2CppObjectBase obj, FinalizerContainer finalizer)
    {
        GC.SuppressFinalize(obj);
        obj.finalizerState = Il2CppObjectFinalizerState.NotYet;
        s_finalizers.Add(obj, finalizer);
    }
}

public static class Il2CppObjectPool
{
    internal static bool DisableCaching { get; set; }

    private static readonly ConcurrentDictionary<IntPtr, WeakReference<Il2CppObjectBase>> s_cache = new();

    internal static void Remove(IntPtr ptr)
    {
        s_cache.TryRemove(ptr, out _);
    }

    internal static void Free(nint unmanagedGcHandle, IntPtr ptr)
    {
        IL2CPP.il2cpp_gchandle_free(unmanagedGcHandle);
        if (ptr != IntPtr.Zero) Il2CppObjectPool.Remove(ptr);
    }

    public static void InternWeak(Il2CppObjectBase obj)
    {
        IntPtr ptr = obj.Pointer;
        obj.pooledPtr = ptr;
        s_cache[ptr] = new(obj);
    }

    internal static bool ReferenceIsDead(IntPtr ptr)
    {
        return s_cache.TryGetValue(ptr, out var reference) && !reference.TryGetTarget(out _);
    }

    public static T Get<T>(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero || Il2CppFinalizers.s_dying.ContainsKey(ptr)) return default;

        if (DisableCaching) return Il2CppObjectBase.InitializerStore<T>.Initializer(ptr);

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
        if (il2CppObjectBase.Pointer != ptr) Logger.Instance.LogError("Pointer interned at wrong address!");
        InternWeak(il2CppObjectBase);
        return newObj;
    }
}
