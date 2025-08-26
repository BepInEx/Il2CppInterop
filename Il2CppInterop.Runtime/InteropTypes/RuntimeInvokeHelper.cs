using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public static unsafe class RuntimeInvokeHelper
{
    private static IntPtr Il2CppRuntimeInvoke(IntPtr method, IntPtr obj, void** parameters)
    {
        IntPtr exception = default;
        IntPtr result = IL2CPP.il2cpp_runtime_invoke(method, obj, parameters, ref exception);
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
        IntPtr result = Il2CppRuntimeInvoke(method, obj, parameters);
        if (IsPointerOrByRef<TResult>())
        {
            // Pointers and by refs are returned directly.
            return Unsafe.As<IntPtr, TResult>(ref result);
        }
        else if (IsValueType<TResult>())
        {
            // This is a performance optimization. The other code path would also return the correct result.
            byte* data = (byte*)IL2CPP.il2cpp_object_unbox(result);
            return Il2CppTypeHelper.ReadFromPointer<TResult>(data);
        }
        else
        {
            return (TResult?)Il2CppObjectPool.Get(result);
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

    public static int RequiredStackAllocationSize<T>()
        where T : IIl2CppType
    {
        if (IsPointer<T>())
        {
            return default;
        }
        else if (IsByRef<T>())
        {
            return ((IIl2CppByReference)default(T)!).ReferenceSize;
        }
        else if (IsValueType<T>())
        {
            return T.Size;
        }
        else
        {
            return default;
        }
    }

    public static IntPtr PrepareParameter<T>(T? parameter, byte* parameter_stackAllocData)
         where T : IIl2CppType<T>
    {
        if (IsPointer<T>())
        {
            return Unsafe.As<T, IntPtr>(ref parameter!);
        }
        else if (IsByRef<T>())
        {
            var il2CppByRef = ((IIl2CppByReference)parameter!);
            il2CppByRef.WriteReferenceToSpan(new Span<byte>(parameter_stackAllocData, il2CppByRef.ReferenceSize));
            return (IntPtr)parameter_stackAllocData;
        }
        else if (IsValueType<T>())
        {
            parameter!.WriteToPointer(parameter_stackAllocData);
            return (IntPtr)parameter_stackAllocData;
        }
        else
        {
            return parameter.Box();
        }
    }

    public static void CleanupParameter<T>(T? parameter, byte* parameter_stackAllocData)
    {
        if (IsByRef<T>())
        {
            var il2CppByRef = (IIl2CppByReference)parameter!;
            il2CppByRef.ReadReferenceFromSpan(new ReadOnlySpan<byte>(parameter_stackAllocData, il2CppByRef.ReferenceSize));
        }
    }
}
