using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
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

    /// <summary>
    /// Gets the address of a static field in the static field data of its class.
    /// </summary>
    /// <remarks>
    /// This requires <c>il2cpp_class_get_static_field_data</c>, which Il2Cpp exports from Unity 2021.
    /// </remarks>
    public static ByReference<T> GetStaticFieldAddress<T>(nint fieldInfo) where T : IIl2CppType<T>
    {
        ArgumentNullException.ThrowIfNull(fieldInfo.ToPointer(), nameof(fieldInfo));
        var declaringClass = IL2CPP.il2cpp_field_get_parent(fieldInfo);

        // As for any access to a static field, the static constructor runs first. Class initialization also allocates the static field data.
        IL2CPP.il2cpp_runtime_class_init(declaringClass);

        var offset = IL2CPP.il2cpp_field_get_offset(fieldInfo);
        if (offset == ThreadStaticFieldOffset)
            throw new NotSupportedException($"Thread static field {IL2CPP.il2cpp_field_get_name(fieldInfo)} has no address in the static field data");

        return new ByReference<T>((byte*)IL2CPP.il2cpp_class_get_static_field_data(declaringClass) + offset);
    }

    // THREAD_STATIC_FIELD_OFFSET in libil2cpp
    private const uint ThreadStaticFieldOffset = unchecked((uint)-1);

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

    public static GCHandle<T> GCHandleFromIntPtr<T>(nint ptr) where T : class?
    {
        return ptr is 0 ? default : GCHandle<T>.FromIntPtr(ptr);
    }

    public static nint GCHandleToIntPtr<T>(GCHandle<T> handle) where T : class?
    {
        return handle.IsAllocated ? GCHandle<T>.ToIntPtr(handle) : nint.Zero;
    }

    public static T GCHandleGetTarget<T>(GCHandle<T> handle) where T : class?
    {
        return handle.IsAllocated ? handle.Target : null!;
    }

    private static class FunctionPointerCache<T> where T : IIl2CppType<T>
    {
        public static readonly delegate*<Object, int, T?, void> SetInstanceFieldValue = IL2CPP.UsesWriteBarriers
            ? &FieldAccess.SetInstanceFieldValue_WriteBarrier
            : &FieldAccess.SetInstanceFieldValue_Pointer;
    }
}
