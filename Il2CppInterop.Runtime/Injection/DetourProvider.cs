using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Startup;

namespace Il2CppInterop.Runtime.Injection;

public interface IDetour : IDisposable
{
    nint Target { get; }
    nint Detour { get; }
    nint OriginalTrampoline { get; }

    void Apply();
}

public interface IDetourProvider
{
    IDetour Create(nint original, nint target);
}

internal static class Detour
{
    public static T Apply<T>(nint original, T target) where T : Delegate
    {
        var toPtr = Marshal.GetFunctionPointerForDelegate(target);
        var detour = Il2CppInteropRuntime.Instance.DetourProvider.Create(original, toPtr);
        detour.Apply();
        return Marshal.GetDelegateForFunctionPointer<T>(detour.OriginalTrampoline);
    }
}
