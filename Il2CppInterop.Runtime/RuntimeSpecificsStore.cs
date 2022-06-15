using System;
using System.Collections.Generic;
using System.Threading;

namespace Il2CppInterop.Runtime;

public static class RuntimeSpecificsStore
{
    private static readonly ReaderWriterLockSlim Lock = new();
    private static readonly Dictionary<IntPtr, bool> WasInjectedStore = new();

    public static bool IsInjected(IntPtr nativeClass)
    {
        Lock.EnterReadLock();
        try
        {
            return WasInjectedStore.TryGetValue(nativeClass, out var result) && result;
        }
        finally
        {
            Lock.ExitReadLock();
        }
    }

    public static void SetClassInfo(IntPtr nativeClass, bool wasInjected)
    {
        Lock.EnterWriteLock();
        try
        {
            WasInjectedStore[nativeClass] = wasInjected;
        }
        finally
        {
            Lock.ExitWriteLock();
        }
    }
}
