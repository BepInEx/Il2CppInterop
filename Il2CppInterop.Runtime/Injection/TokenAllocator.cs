using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Il2CppInterop.Runtime.Injection;

internal static class TokenAllocator
{
    private static long s_LastInjectedToken = -2;
    private static readonly ConcurrentDictionary<long, IntPtr> s_InjectedClasses = new();

    public static long Assign(IntPtr classPointer)
    {
        var newToken = Interlocked.Decrement(ref s_LastInjectedToken);
        s_InjectedClasses[newToken] = classPointer;
        return newToken;
    }

    public static bool TryGetClassPointer(long token, out IntPtr classPointer)
    {
        return s_InjectedClasses.TryGetValue(token, out classPointer);
    }
}
