using System;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.InteropTypes;

public static unsafe class FieldAccessHelper
{
    public static T? GetStaticFieldValue<T>(IntPtr fieldInfoPtr) where T : IIl2CppType<T>
    {
        var data = stackalloc byte[T.Size];
        IL2CPP.il2cpp_field_static_get_value(fieldInfoPtr, data);
        return T.ReadFromSpan(new ReadOnlySpan<byte>(data, T.Size));
    }

    public static void SetStaticFieldValue<T>(IntPtr fieldInfoPtr, T? value) where T : IIl2CppType<T>
    {
        if (typeof(T).IsValueType)
        {
            var data = stackalloc byte[T.Size];
            value.WriteToPointer(data);
            IL2CPP.il2cpp_field_static_set_value(fieldInfoPtr, data);
        }
        else
        {
            IL2CPP.il2cpp_field_static_set_value(fieldInfoPtr, (void*)value.Box());
        }
    }

    public static T? GetInstanceFieldValue<T>(IIl2CppObjectBase instance, int fieldOffset) where T : IIl2CppType<T>
    {
        var data = (byte*)instance.Pointer + fieldOffset;
        return Il2CppTypeHelper.ReadFromPointer<T>(data);
    }

    public static void SetInstanceFieldValue<T>(IIl2CppObjectBase instance, int fieldOffset, T? value) where T : IIl2CppType<T>
    {
        FunctionPointerCache<T>.SetInstanceFieldValue(instance, fieldOffset, value);
    }

    public static void SetInstanceFieldValue_Wbarrior<T>(IIl2CppObjectBase instance, int fieldOffset, T? value) where T : IIl2CppType<T>
    {
        var data = (byte*)instance.Pointer + fieldOffset;
        if (typeof(T).IsValueType)
        {
            value!.WriteToPointer(data);
        }
        else
        {
            IL2CPP.il2cpp_gc_wbarrier_set_field(instance.Pointer, (IntPtr)data, value.Box());
        }
    }

    public static void SetInstanceFieldValue_Pointer<T>(IIl2CppObjectBase instance, int fieldOffset, T? value) where T : IIl2CppType<T>
    {
        var data = (byte*)instance.Pointer + fieldOffset;
        if (typeof(T).IsValueType)
        {
            value!.WriteToPointer(data);
        }
        else
        {
            *(IntPtr*)data = value.Box();
        }
    }

    private static bool HasWbarriorSupport()
    {
        if (NativeLibrary.TryLoad("GameAssembly", out var handle))
        {
            var result = NativeLibrary.TryGetExport(handle, "il2cpp_gc_wbarrier_set_field", out _);
            NativeLibrary.Free(handle);
            return result;
        }
        return false;
    }

    private static bool WbarriorSupport { get; } = HasWbarriorSupport();

    private static class FunctionPointerCache<T> where T : IIl2CppType<T>
    {
        public static readonly delegate*<IIl2CppObjectBase, int, T?, void> SetInstanceFieldValue = WbarriorSupport
            ? &FieldAccessHelper.SetInstanceFieldValue_Wbarrior
            : &FieldAccessHelper.SetInstanceFieldValue_Pointer;
    }
}
