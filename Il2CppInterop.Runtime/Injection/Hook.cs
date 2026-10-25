using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection.Hooks;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection;

internal static class Hook
{
    private static readonly MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook GetTypeInfoFromTypeDefinitionIndexHook = new();
    private static readonly Class_GetFieldDefaultValue_Hook GetFieldDefaultValueHook = new();
    private static readonly Class_FromIl2CppType_Hook FromIl2CppTypeHook = new();
    private static readonly Class_FromName_Hook FromNameHook = new();

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    internal static void ApplyInjectionHooks()
    {
        GetTypeInfoFromTypeDefinitionIndexHook.ApplyHook();
        GetFieldDefaultValueHook.ApplyHook();
        FromIl2CppTypeHook.ApplyHook();
        FromNameHook.ApplyHook();
    }

    public static IDisposable ApplyDetour<T>(nint original, T target, out T trampoline) where T : Delegate
    {
        return Il2CppInteropRuntime.Instance.DetourProvider.Create(original, target, out trampoline);
    }
}
internal abstract class Hook<T> where T : Delegate
{
#nullable disable
    private bool _isApplied;
    private T _detour;
    private T _method;
    private T _original;
#nullable restore

    public T Original => _original;

    public abstract string TargetMethodName { get; }

    public abstract T GetDetour();

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public abstract IntPtr FindTargetMethod();

    public virtual void TargetMethodNotFound()
    {
        throw new Exception($"Required target method {TargetMethodName} not found");
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public void ApplyHook()
    {
        if (_isApplied) return;

        var methodPtr = FindTargetMethod();

        if (methodPtr == IntPtr.Zero)
        {
            TargetMethodNotFound();
            return;
        }

        Logger.Instance.LogTrace("{MethodName} found: 0x{MethodPtr}", TargetMethodName, methodPtr.ToInt64().ToString("X2"));

        _detour = GetDetour();
        Hook.ApplyDetour(methodPtr, _detour, out _original);
        _method = Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
        _isApplied = true;
    }
}
