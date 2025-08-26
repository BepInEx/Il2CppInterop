using System;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public static unsafe class FieldAccessHelper
{
    public static T? GetStaticFieldValue<T>(IntPtr fieldInfoPtr) where T : IIl2CppValueType
    {
        if (typeof(T).IsValueType)
        {
            T result = default!;
            byte* data = stackalloc byte[result.Size];
            IL2CPP.il2cpp_field_static_get_value(fieldInfoPtr, data);
            result.ReadFromSpan(new ReadOnlySpan<byte>(data, result.Size));
            return result;
        }
        else
        {
            IntPtr returnedPtr = default;
            IL2CPP.il2cpp_field_static_get_value(fieldInfoPtr, &returnedPtr);
            return (T?)Il2CppObjectPool.Get(returnedPtr);
        }
    }

    public static void SetStaticFieldValue<T>(IntPtr fieldInfoPtr, T? value) where T : IIl2CppValueType
    {
        if (typeof(T).IsValueType)
        {
            byte* data = stackalloc byte[value!.Size];
            value!.WriteToPointer(data);
            IL2CPP.il2cpp_field_static_set_value(fieldInfoPtr, data);
        }
        else
        {
            IL2CPP.il2cpp_field_static_set_value(fieldInfoPtr, (void*)value.Box());
        }
    }

    public static T? GetInstanceFieldValue<T>(IIl2CppObjectBase instance, int fieldOffset) where T : IIl2CppValueType
    {
        if (typeof(T).IsValueType)
        {
            T result = default!;
            byte* data = (byte*)instance.Pointer + fieldOffset;
            result.ReadFromSpan(new ReadOnlySpan<byte>(data, result.Size));
            return result;
        }
        else
        {
            byte* data = (byte*)instance.Pointer + fieldOffset;
            IntPtr returnedPtr = *(IntPtr*)data;
            return (T?)Il2CppObjectPool.Get(returnedPtr);
        }
    }

    public static void SetInstanceFieldValue_Wbarrior<T>(IIl2CppObjectBase instance, int fieldOffset, T? value) where T : IIl2CppValueType
    {
        byte* data = (byte*)instance.Pointer + fieldOffset;
        if (typeof(T).IsValueType)
        {
            value!.WriteToPointer(data);
        }
        else
        {
            IL2CPP.il2cpp_gc_wbarrier_set_field(instance.Pointer, (IntPtr)data, value.Box());
        }
    }

    public static void SetInstanceFieldValue_Pointer<T>(IIl2CppObjectBase instance, int fieldOffset, T? value) where T : IIl2CppValueType
    {
        byte* data = (byte*)instance.Pointer + fieldOffset;
        if (typeof(T).IsValueType)
        {
            value!.WriteToPointer(data);
        }
        else
        {
            *(IntPtr*)data = value.Box();
        }
    }
}
