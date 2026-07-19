using System;

namespace Il2CppInterop.Runtime.Injection;

public interface IDetourProvider
{
    IDisposable Create<TDelegate>(nint original, TDelegate target, out TDelegate trampoline) where TDelegate : Delegate;
}
