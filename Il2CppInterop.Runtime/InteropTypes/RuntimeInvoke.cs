using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public static unsafe class RuntimeInvoke
{
    private static IntPtr Il2CppRuntimeInvoke(IntPtr method, IntPtr obj, void** parameters)
    {
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
            var data = (byte*)IL2CPP.il2cpp_object_unbox(result);
            return Il2CppTypeHelper.ReadFromPointer<TResult>(data);
        }
        else
        {
            return (TResult?)Il2CppObjectPool.Get(result);
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
            return @this.GetValue().Box();
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
}
