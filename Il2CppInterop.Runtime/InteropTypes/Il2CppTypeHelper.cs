using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public static class Il2CppTypeHelper
{
    public static bool IsBlittable<T>() where T : IIl2CppType
    {
        return T.Size == Unsafe.SizeOf<T>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<T>()
            && !typeof(T).IsGenericType;
    }

    public static int SizeOf<T>() where T : IIl2CppType
    {
        return T.Size;
    }

    public static void WriteToSpan<T>(this T? value, Span<byte> span) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, span);
    }

    public static T? ReadFromSpan<T>(ReadOnlySpan<byte> span) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(span);
    }

    public static void WriteToSpanAtOffset<T>(this T? value, Span<byte> span, int offset) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, span.Slice(offset, T.Size));
    }

    public static T? ReadFromSpanAtOffset<T>(ReadOnlySpan<byte> span, int offset) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(span.Slice(offset, T.Size));
    }

    public static void WriteToSpanBlittable<T>(T value, Span<byte> span) where T : unmanaged
    {
        MemoryMarshal.Write(span, in value);
    }

    public static T ReadFromSpanBlittable<T>(ReadOnlySpan<byte> span) where T : unmanaged
    {
        return MemoryMarshal.Read<T>(span);
    }

    public static unsafe void WriteToPointer<T>(this T? value, void* ptr) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, new Span<byte>(ptr, T.Size));
    }

    public static unsafe T? ReadFromPointer<T>(void* ptr) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(new ReadOnlySpan<byte>(ptr, T.Size));
    }

    public static T? ReadReference<T>(ReadOnlySpan<byte> span) where T : IIl2CppType<T>
    {
        return (T?)Il2CppObjectPool.Get(ReadPointer(span));
    }

    public static void WriteReference<T>(T? value, Span<byte> span) where T : IIl2CppType<T>
    {
        WritePointer(value.Box(), span);
    }

    public static void WriteClass(IIl2CppObjectBase? value, Span<byte> span)
    {
        if (value == null)
        {
            WritePointer(IntPtr.Zero, span);
        }
        else
        {
            WritePointer(value.Pointer, span);
        }
    }

    public static nint ReadPointer(ReadOnlySpan<byte> span)
    {
        if (BitConverter.IsLittleEndian)
        {
            return BinaryPrimitives.ReadIntPtrLittleEndian(span);
        }
        else
        {
            return BinaryPrimitives.ReadIntPtrBigEndian(span);
        }
    }

    public static void WritePointer(nint pointer, Span<byte> span)
    {
        if (BitConverter.IsLittleEndian)
        {
            BinaryPrimitives.WriteIntPtrLittleEndian(span, pointer);
        }
        else
        {
            BinaryPrimitives.WriteIntPtrBigEndian(span, pointer);
        }
    }

    public static unsafe IntPtr Box<T>(this T? value) where T : IIl2CppType<T>
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
            byte* data = stackalloc byte[T.Size];
            WriteToPointer(value, data);
            IntPtr boxedPtr = IL2CPP.il2cpp_value_box(value.ObjectClass, (IntPtr)data);
            return boxedPtr;
        }
        else
        {
            throw new InvalidCastException();
        }
    }
}
