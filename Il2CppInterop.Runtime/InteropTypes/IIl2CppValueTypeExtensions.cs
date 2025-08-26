using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public static unsafe class IIl2CppValueTypeExtensions
{
    public static void WriteToPointer<T>(this T value, void* data) where T : IIl2CppValueType
    {
        value.WriteToSpan(new Span<byte>(data, value.Size));
    }

    public static void ReadFromPointer<T>(this ref T value, void* data) where T : struct, IIl2CppValueType
    {
        value.ReadFromSpan(new ReadOnlySpan<byte>(data, value.Size));
    }

    public static IntPtr Box<T>(this T? value) where T : IIl2CppValueType
    {
        if (value is null)
        {
            return IntPtr.Zero;
        }
        else if (value is IIl2CppObjectBase @object)
        {
            return @object.Pointer;
        }
        else if (value.GetType().IsValueType)
        {
            byte* data = stackalloc byte[value.Size];
            value.WriteToSpan(new Span<byte>(data, value.Size));
            IntPtr boxedPtr = IL2CPP.il2cpp_value_box(value.ObjectClass, (IntPtr)data);
            return boxedPtr;
        }
        else
        {
            throw new InvalidCastException();
        }
    }
}
