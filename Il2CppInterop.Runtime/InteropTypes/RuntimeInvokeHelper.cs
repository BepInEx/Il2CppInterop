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
        where TResult : IIl2CppValueType
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
            TResult returnValue = default!;
            byte* data = (byte*)IL2CPP.il2cpp_object_unbox(result);
            returnValue.ReadFromSpan(new ReadOnlySpan<byte>(data, returnValue.Size));
            return returnValue;
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
        where T : IIl2CppValueType
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
            return default(T)!.Size;
        }
        else
        {
            return default;
        }
    }

    public static IntPtr PrepareParameter<T>(T? parameter, byte* parameter_stackAllocData)
         where T : IIl2CppValueType
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
         where T : IIl2CppValueType
    {
        if (IsByRef<T>())
        {
            var il2CppByRef = ((IIl2CppByReference)parameter!);
            il2CppByRef.ReadReferenceFromSpan(new ReadOnlySpan<byte>(parameter_stackAllocData, il2CppByRef.ReferenceSize));
        }
    }

    private static void ExampleInvokeAction<T1, T2>(IntPtr method, IntPtr obj, T1? param1, T2? param2)
         where T1 : IIl2CppValueType
         where T2 : IIl2CppValueType
    {
        IntPtr* parameters = stackalloc IntPtr[2];

        // Parameter 1
        int param1_stackAllocSize = RequiredStackAllocationSize<T1>();
        byte* param1_stackAllocData;
        if (param1_stackAllocSize > 0)
        {
            byte* temp = stackalloc byte[param1_stackAllocSize];
            param1_stackAllocData = temp;
        }
        else
        {
            param1_stackAllocData = null;
        }
        parameters[0] = PrepareParameter(param1, param1_stackAllocData);

        // Parameter 2
        int param2_stackAllocSize = RequiredStackAllocationSize<T1>();
        byte* param2_stackAllocData;
        if (param2_stackAllocSize > 0)
        {
            byte* temp = stackalloc byte[param2_stackAllocSize];
            param2_stackAllocData = temp;
        }
        else
        {
            param2_stackAllocData = null;
        }
        parameters[1] = PrepareParameter(param2, param2_stackAllocData);

        InvokeAction(method, obj, (void**)parameters);

        CleanupParameter(param1, param1_stackAllocData);
        CleanupParameter(param2, param2_stackAllocData);
    }

    private static TResult? ExampleInvokeFunction<T1, T2, TResult>(IntPtr method, IntPtr obj, T1? param1, T2? param2)
         where T1 : IIl2CppValueType
         where T2 : IIl2CppValueType
         where TResult : IIl2CppValueType
    {
        IntPtr* parameters = stackalloc IntPtr[2];

        // Parameter 1
        int param1_stackAllocSize = RequiredStackAllocationSize<T1>();
        byte* param1_stackAllocData;
        if (param1_stackAllocSize > 0)
        {
            byte* temp = stackalloc byte[param1_stackAllocSize];
            param1_stackAllocData = temp;
        }
        else
        {
            param1_stackAllocData = null;
        }
        parameters[0] = PrepareParameter(param1, param1_stackAllocData);

        // Parameter 2
        int param2_stackAllocSize = RequiredStackAllocationSize<T1>();
        byte* param2_stackAllocData;
        if (param2_stackAllocSize > 0)
        {
            byte* temp = stackalloc byte[param2_stackAllocSize];
            param2_stackAllocData = temp;
        }
        else
        {
            param2_stackAllocData = null;
        }
        parameters[1] = PrepareParameter(param2, param2_stackAllocData);

        var result = InvokeFunction<TResult>(method, obj, (void**)parameters);

        CleanupParameter(param1, param1_stackAllocData);
        CleanupParameter(param2, param2_stackAllocData);

        return result;
    }

    private static TResult? ExampleInvokeFunction<TResult>(IntPtr method, IntPtr obj)
         where TResult : IIl2CppValueType
    {
        return InvokeFunction<TResult>(method, obj, null);
    }
}
