using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

public static unsafe class FieldAccess
{
    public static T? GetStaticFieldValue<T>(nint fieldInfo) where T : IIl2CppType<T>
    {
        ArgumentNullException.ThrowIfNull(fieldInfo.ToPointer(), nameof(fieldInfo));
        var data = stackalloc byte[T.Size];
        IL2CPP.il2cpp_field_static_get_value(fieldInfo, data);
        return T.ReadFromSpan(new ReadOnlySpan<byte>(data, T.Size));
    }

    public static void SetStaticFieldValue<T>(nint fieldInfo, T? value) where T : IIl2CppType<T>
    {
        ArgumentNullException.ThrowIfNull(fieldInfo.ToPointer(), nameof(fieldInfo));
        if (typeof(T).IsValueType)
        {
            var data = stackalloc byte[T.Size];
            value.WriteToPointer(data);
            IL2CPP.il2cpp_field_static_set_value(fieldInfo, data);
        }
        else
        {
            IL2CPP.il2cpp_field_static_set_value(fieldInfo, (void*)NativeBoxing.Box(value));
        }
    }

    public static T? GetInstanceFieldValue<T>(Object instance, int fieldOffset) where T : IIl2CppType<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldOffset);
        var data = (byte*)instance.Pointer + fieldOffset;
        return Il2CppType.ReadFromPointer<T>(data);
    }

    public static void SetInstanceFieldValue<T>(Object instance, int fieldOffset, T? value) where T : IIl2CppType<T>
    {
        FunctionPointerCache<T>.SetInstanceFieldValue(instance, fieldOffset, value);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetInstanceFieldValue_WriteBarrier<T>(Object instance, int fieldOffset, T? value) where T : IIl2CppType<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldOffset);
        var data = (byte*)instance.Pointer + fieldOffset;
        if (typeof(T).IsValueType)
        {
            value.WriteToPointer(data);
        }
        else
        {
            IL2CPP.il2cpp_gc_wbarrier_set_field(instance.Pointer, (nint)data, (nint)NativeBoxing.Box(value));
        }
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void SetInstanceFieldValue_Pointer<T>(Object instance, int fieldOffset, T? value) where T : IIl2CppType<T>
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fieldOffset);
        var data = (byte*)instance.Pointer + fieldOffset;
        if (typeof(T).IsValueType)
        {
            value.WriteToPointer(data);
        }
        else
        {
            *(nint*)data = (nint)NativeBoxing.Box(value);
        }
    }

    public static nint GetFieldInfo(nint classPointer, string fieldName)
    {
        if (classPointer == nint.Zero)
            return nint.Zero;

        var field = IL2CPP.il2cpp_class_get_field_from_name(classPointer, fieldName);
        if (field == nint.Zero)
            Logger.Instance.LogError("Field {FieldName} was not found on class {ClassName}", fieldName, IL2CPP.il2cpp_class_get_name(classPointer));
        return field;
    }

    public static int GetFieldOffset(nint fieldInfo)
    {
        if (fieldInfo == nint.Zero)
            return -1;
        return (int)IL2CPP.il2cpp_field_get_offset(fieldInfo);
    }

    private static bool HasWriteBarrierSupport()
    {
        if (NativeLibrary.TryLoad("GameAssembly", out var handle))
        {
            var result = NativeLibrary.TryGetExport(handle, "il2cpp_gc_wbarrier_set_field", out _);
            NativeLibrary.Free(handle);
            return result;
        }
        return false;
    }

    private static bool WriteBarrierSupport { get; } = HasWriteBarrierSupport();

    private static class FunctionPointerCache<T> where T : IIl2CppType<T>
    {
        public static readonly delegate*<Object, int, T?, void> SetInstanceFieldValue = WriteBarrierSupport
            ? &FieldAccess.SetInstanceFieldValue_WriteBarrier
            : &FieldAccess.SetInstanceFieldValue_Pointer;
    }
}
