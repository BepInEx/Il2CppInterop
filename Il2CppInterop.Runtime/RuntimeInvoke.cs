using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Microsoft.Extensions.Logging;
using Il2CppException = Il2CppInterop.Runtime.Exceptions.Il2CppException;

namespace Il2CppInterop.Runtime;

public static unsafe class RuntimeInvoke
{
    private static IntPtr Il2CppRuntimeInvoke(IntPtr method, IntPtr obj, void** parameters)
    {
        ArgumentNullException.ThrowIfNull(method.ToPointer(), nameof(method));
        IntPtr exception = default;
        var result = IL2CPP.il2cpp_runtime_invoke(method, obj, parameters, ref exception);
        Il2CppException.RaiseExceptionIfNecessary(exception);
        return result;
    }

    public static void InvokeAction(IntPtr method, IntPtr obj, void** parameters)
    {
        Il2CppRuntimeInvoke(method, obj, parameters);
    }

    public static TResult? InvokeFunction<TResult>(IntPtr method, IntPtr obj, void** parameters)
        where TResult : IIl2CppType<TResult>
    {
        var result = Il2CppRuntimeInvoke(method, obj, parameters);
        if (IsPointerOrByRef<TResult>())
        {
            // Pointers and by refs are returned directly.
            return Unsafe.As<IntPtr, TResult>(ref result);
        }
        else if (IsValueType<TResult>())
        {
            // This is a performance optimization. The other code path would also return the correct result.
            return TResult.UnboxNative((ObjectPointer)result);
        }
        else
        {
            return TResult.Unbox(Il2CppObjectPool.Get(result));
        }
    }

    public static IntPtr GetPointerForThis<T>(ByReference<T> @this)
        where T : IIl2CppType<T>
    {
        if (typeof(T).IsValueType)
        {
            return new IntPtr(@this.ToPointer());
        }
        else
        {
            return (IntPtr)NativeBoxing.Box(@this.GetValue());
        }
    }

    public static IntPtr GetPointerForParameter<T>(ByReference<T> parameter)
        where T : IIl2CppType<T>
    {
        if (IsPointerOrByRef<T>())
        {
            // Pointer to pointer, which is passed directly
            return *(IntPtr*)parameter.ToPointer();
        }
        else if (IsValueType<T>())
        {
            // Pointer to value type data
            return new IntPtr(parameter.ToPointer());
        }
        else
        {
            // Pointer to object pointer, which is passed directly
            return *(IntPtr*)parameter.ToPointer();
        }
    }

    private static bool IsPointerOrByRef<T>()
    {
        return IsPointer<T>() || IsByRef<T>();
    }

    private static bool IsPointer<T>()
    {
        return typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Pointer<>);
    }

    private static bool IsByRef<T>()
    {
        return typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(ByReference<>);
    }

    private static bool IsValueType<T>()
    {
        return typeof(T).IsValueType;
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public static T ResolveICall<T>(string signature) where T : Delegate
    {
        var icallPtr = IL2CPP.il2cpp_resolve_icall(signature);
        if (icallPtr == nint.Zero)
        {
            Logger.Instance.LogTrace("ICall {Signature} not resolved", signature);
            return GenerateDelegateForMissingICall<T>(signature);
        }

        return GenerateDelegateForICall<T>(icallPtr);
    }

    [RequiresDynamicCode("")]
    private static T GenerateDelegateForMissingICall<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(string signature) where T : Delegate
    {
        var invoke = typeof(T).GetMethod("Invoke")!;

        var trampoline = new DynamicMethod("(missing icall delegate) " + typeof(T).FullName,
            invoke.ReturnType, invoke.GetParameters().Select(it => it.ParameterType).ToArray(), typeof(RuntimeInvoke), true);
        var bodyBuilder = trampoline.GetILGenerator();

        bodyBuilder.Emit(OpCodes.Ldstr, $"ICall with signature {signature} was not resolved");
        bodyBuilder.Emit(OpCodes.Newobj, typeof(Exception).GetConstructor([typeof(string)])!);
        bodyBuilder.Emit(OpCodes.Throw);

        return (T)trampoline.CreateDelegate(typeof(T));
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static T GenerateDelegateForICall<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] T>(nint icallPtr) where T : Delegate
    {
        var invoke = typeof(T).GetMethod("Invoke")!;

        var trampoline = new DynamicMethod("(icall delegate) " + typeof(T).FullName,
            invoke.ReturnType, invoke.GetParameters().Select(it => it.ParameterType).ToArray(), typeof(RuntimeInvoke), true);
        var bodyBuilder = trampoline.GetILGenerator();

        var parameters = invoke.GetParameters();
        var parameterTypes = new Type[parameters.Length];
        var locals = new LocalBuilder[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            var parameterType = parameter.ParameterType;

            var nativeStruct = TrampolineBuilder.GetNativeType(parameterType);
            parameterTypes[i] = nativeStruct;

            var nativeLocal = bodyBuilder.DeclareLocal(nativeStruct);
            locals[i] = nativeLocal;

            bodyBuilder.Emit(OpCodes.Ldarg, i);
            bodyBuilder.Emit(OpCodes.Ldloca, nativeLocal);
            bodyBuilder.Emit(OpCodes.Call, typeof(Il2CppType).GetMethod(nameof(Il2CppType.WriteToPointer))!.MakeGenericMethod(parameterType));
        }

        foreach (var local in locals)
        {
            bodyBuilder.Emit(OpCodes.Ldloc, local);
        }

        bodyBuilder.Emit(OpCodes.Ldc_I8, icallPtr);
        bodyBuilder.Emit(OpCodes.Conv_I);

        if (invoke.ReturnType == typeof(void))
        {
            bodyBuilder.EmitCalli(OpCodes.Calli, CallingConventions.Standard, typeof(void), parameterTypes, null);
        }
        else
        {
            var returnType = invoke.ReturnType;
            var nativeStruct = TrampolineBuilder.GetNativeType(returnType);

            bodyBuilder.EmitCalli(OpCodes.Calli, CallingConventions.Standard, nativeStruct, parameterTypes, null);

            var returnLocal = bodyBuilder.DeclareLocal(nativeStruct);
            bodyBuilder.Emit(OpCodes.Stloc, returnLocal);

            bodyBuilder.Emit(OpCodes.Ldloca, returnLocal);
            bodyBuilder.Emit(OpCodes.Call, typeof(Il2CppType).GetMethod(nameof(Il2CppType.ReadFromPointer))!.MakeGenericMethod(returnType));
        }
        bodyBuilder.Emit(OpCodes.Ret);

        return (T)trampoline.CreateDelegate(typeof(T));
    }
}
