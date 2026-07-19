using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Structs;

namespace Il2CppInterop.Runtime;

public static class DelegateSupport
{
    private static readonly ConcurrentDictionary<Type, Delegate> NativeToManagedTrampolines = new();

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static Delegate GetOrCreateNativeToManagedTrampoline([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type delegateType)
    {
        return NativeToManagedTrampolines.GetOrAdd(delegateType, CreateNativeToManagedTrampoline);
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static Delegate CreateNativeToManagedTrampoline([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type delegateType)
    {
        var invokeMethod = Il2CppToMonoDelegateReference.GetOrCreateInvokeMethod(delegateType);
        return TrampolineBuilder.CreateTrampoline(invokeMethod, false);
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public static TIl2Cpp? ConvertDelegate<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TIl2Cpp>(Delegate @delegate) where TIl2Cpp : Il2CppSystem.Delegate, IIl2CppType<TIl2Cpp>
    {
        if (@delegate == null)
            return null;

        var managedInvokeMethod = @delegate.GetType().GetMethod("Invoke")!;
        var parameterInfos = managedInvokeMethod.GetParameters();
        foreach (var parameterInfo in parameterInfos)
        {
            var parameterType = parameterInfo.ParameterType;
            if (parameterType.IsGenericParameter)
                throw new ArgumentException(
                    $"Delegate has unsubstituted generic parameter ({parameterType}) which is not supported");
        }

        var classTypePtr = Il2CppType.GetClassPointer<TIl2Cpp>();
        if (classTypePtr == IntPtr.Zero)
            throw new ArgumentException($"Type {typeof(TIl2Cpp)} has uninitialized class pointer");

        var il2CppDelegateType = Il2CppSystem.Type.FromClassPointer(classTypePtr);
        var nativeDelegateInvokeMethod = il2CppDelegateType.GetMethod("Invoke");

        var nativeParameters = nativeDelegateInvokeMethod.GetParameters();
        if (nativeParameters.Count != parameterInfos.Length)
            throw new ArgumentException(
                $"Managed delegate has {parameterInfos.Length} parameters, native has {nativeParameters.Count}, these should match");

        for (var i = 0; i < nativeParameters.Count; i++)
        {
            var nativeType = nativeParameters[i].ParameterType;

            var typePointerForManagedType = Il2CppTypePointerStore.GetNativeTypePointer(parameterInfos[i].ParameterType);
            var managedType = Il2CppSystem.Type.FromTypePointer(typePointerForManagedType);

            if (nativeType != managedType)
                throw new ArgumentException(
                    $"Parameter type at {i} has mismatched native type pointers; types: {nativeType?.FullName} != {managedType?.FullName}");
        }

        var managedTrampoline = GetOrCreateNativeToManagedTrampoline(@delegate.GetType());

        var methodInfo = UnityVersionHandler.NewMethod();
        methodInfo.MethodPointer = Marshal.GetFunctionPointerForDelegate(managedTrampoline);
        methodInfo.ParametersCount = (byte)parameterInfos.Length;
        methodInfo.Slot = ushort.MaxValue;
        methodInfo.IsMarshalledFromNative = true;

        var delegateReference = new Il2CppToMonoDelegateReference(@delegate, methodInfo.Pointer);

        TIl2Cpp converted = (TIl2Cpp)Activator.CreateInstance(typeof(TIl2Cpp), delegateReference, (Il2CppSystem.IntPtr)methodInfo.Pointer)!;

        converted.method_ptr = methodInfo.MethodPointer;
        converted.method_info = nativeDelegateInvokeMethod; // todo: is this truly a good hack?
        converted.method = methodInfo.Pointer;
        converted.m_target = delegateReference;

        if (UnityVersionHandler.MustUseDelegateConstructor)
        {
            // U2021.2.0+ hack in case the constructor did the wrong thing anyway
            converted.invoke_impl = converted.method_ptr;
            converted.method_code = delegateReference.Pointer;
        }

        return converted;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetParameters")]
    [return: UnsafeAccessorType($"Il2CppInterop.Runtime.InteropTypes.Arrays.{nameof(Il2CppArrayRank1<>)}`1[[Il2CppSystem.Reflection.ParameterInfo, Il2Cppmscorlib]]")]
    private static extern object GetParametersInternal(Il2CppSystem.Reflection.MethodBase method);

    private static IReadOnlyList<Il2CppSystem.Reflection.ParameterInfo> GetParameters(this Il2CppSystem.Reflection.MethodBase method)
    {
        return (IReadOnlyList<Il2CppSystem.Reflection.ParameterInfo>)GetParametersInternal(method);
    }
}
