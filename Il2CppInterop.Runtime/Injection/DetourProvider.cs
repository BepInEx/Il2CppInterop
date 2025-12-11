using System;
using Il2CppInterop.Runtime.Startup;

namespace Il2CppInterop.Runtime.Injection;


public interface IDetourProvider
{
    IDisposable Create<TDelegate>(nint original, TDelegate target, out TDelegate trampoline) where TDelegate : Delegate;
}

internal static class Detour
{
    public static IDisposable Apply<T>(nint original, T target, out T trampoline) where T : Delegate
    {
        return Il2CppInteropRuntime.Instance.DetourProvider.Create(original, target, out trampoline);

    }
}
