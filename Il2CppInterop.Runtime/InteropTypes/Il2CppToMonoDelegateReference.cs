using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.InteropTypes;

[InjectedType]
internal sealed partial class Il2CppToMonoDelegateReference : Object
{
    [Il2CppField]
    public partial Il2CppSystem.IntPtr MethodInfo { get; set; }
    [ManagedField]
    public partial Delegate ReferencedDelegate { get; set; }

    public Il2CppToMonoDelegateReference(Delegate referencedDelegate, IntPtr methodInfo) : this(ObjectPointer.New<Il2CppToMonoDelegateReference>())
    {
        ReferencedDelegate = referencedDelegate;
        MethodInfo = methodInfo;
    }

    [Il2CppFinalizer]
    private void DisposeMethodInfo()
    {
        Marshal.FreeHGlobal(MethodInfo);
        MethodInfo = IntPtr.Zero;
    }

    partial void LogErrorIl2CppFinalize(Exception exception)
    {
        Logger.Instance.LogError($"Exception in {nameof(Il2CppToMonoDelegateReference)}.{nameof(Il2CppFinalize)}: {{Exception}}", exception);
    }

    private static ConcurrentDictionary<Type, MethodInfo> _methodInfoCache = new();

    /// <summary>
    /// Creates or retrieves a cached dynamic method that will cast <see cref="ReferencedDelegate"/>
    /// to the specified <paramref name="delegateType"/> and invoke it with the provided arguments.
    /// </summary>
    /// <remarks>
    /// Due to limitations in dynamic method creation, the dynamic method must be static.
    /// As such, the first parameter of the dynamic method is always <see cref="Il2CppToMonoDelegateReference"/>.
    /// The remaining parameters are copied from the Invoke method of <paramref name="delegateType"/>.
    /// </remarks>
    /// <param name="delegateType">The type of the delegate to cast and invoke.</param>
    /// <returns>The dynamic method.</returns>
    [RequiresDynamicCode("")]
    [RequiresUnreferencedCode("")]
    internal static MethodInfo GetOrCreateInvokeMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type delegateType)
    {
        return _methodInfoCache.GetOrAdd(delegateType, CreateInvokeMethod);
    }

    [RequiresDynamicCode("")]
    private static MethodInfo CreateInvokeMethod([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type delegateType)
    {
        var delegateInvokeMethod = delegateType.GetMethod("Invoke")
            ?? throw new InvalidOperationException($"Delegate type {delegateType.FullName} does not have an Invoke method.");

        var returnType = delegateInvokeMethod.ReturnType;

        // Dynamic methods must be static, so we prepend the declaring type as the first parameter
        Type[] parameterTypes =
        [
            typeof(Il2CppToMonoDelegateReference),
            ..delegateInvokeMethod.GetParameters().Select(p => p.ParameterType)
        ];

        var invokeMethod = new DynamicMethod($"Invoke_{delegateType.FullName}", returnType, parameterTypes,
            typeof(Il2CppToMonoDelegateReference), false);
        var bodyBuilder = invokeMethod.GetILGenerator();

        bodyBuilder.Emit(OpCodes.Ldarg_0);
        bodyBuilder.Emit(OpCodes.Callvirt, typeof(Il2CppToMonoDelegateReference).GetProperty(nameof(ReferencedDelegate))!.GetMethod!);
        bodyBuilder.Emit(OpCodes.Castclass, delegateType);
        for (var i = 1; i < parameterTypes.Length; i++)
        {
            bodyBuilder.Emit(OpCodes.Ldarg, i);
        }

        bodyBuilder.Emit(OpCodes.Callvirt, delegateInvokeMethod);
        bodyBuilder.Emit(OpCodes.Ret);

        return invokeMethod;
    }
}
