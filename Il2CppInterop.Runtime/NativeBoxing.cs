using Il2CppInterop.Common;
using Il2CppSystem;

namespace Il2CppInterop.Runtime;

public static class NativeBoxing
{
    internal static ObjectPointer Box<T>(T? value) where T : IIl2CppType
    {
        return value?.BoxNative() ?? ObjectPointer.Null;
    }

    public static ObjectPointer BoxReferenceType(Object? value)
    {
        return (ObjectPointer?)value?.Pointer ?? ObjectPointer.Null;
    }

    public static unsafe ObjectPointer BoxValueType<T>(in T value) where T : struct, IIl2CppType<T>
    {
        var data = stackalloc byte[T.Size];
        Il2CppType.WriteToPointer(value, data);
        return (ObjectPointer)IL2CPP.il2cpp_value_box(value.ObjectClass, (nint)data);
    }

    public static ObjectPointer BoxNullableValueType<T>(in Nullable<T> value) where T : struct, IIl2CppType<T>, IValueType
    {
        return value.hasValue ? BoxValueType(value.value) : ObjectPointer.Null;
    }

    public static Nullable<T> UnboxNullableValueType<T>(ObjectPointer pointer) where T : struct, IIl2CppType<T>, IValueType
    {
        Nullable<T> result = default;
        if (pointer != ObjectPointer.Null)
        {
            result.hasValue = true;
            result.value = T.UnboxNative(pointer);
        }
        return result;
    }
}
