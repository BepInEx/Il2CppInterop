using System;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
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

    public static void WriteToSpan<T>(T value, Span<byte> span) where T : IIl2CppType<T>
    {
        T.WriteToSpan(value, span);
    }

    public static T ReadFromSpan<T>(ReadOnlySpan<byte> span) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(span);
    }

    public static unsafe T ReadFromPointer<T>(IntPtr ptr) where T : IIl2CppType<T>
    {
        return T.ReadFromSpan(new ReadOnlySpan<byte>(ptr.ToPointer(), T.Size));
    }

    public static T? ReadClass<T>(ReadOnlySpan<byte> span) where T : class, IIl2CppObjectBase
    {
        return (T?)Il2CppObjectPool.Get(ReadPointer(span));
    }

    public static void WriteClass<T>(T? value, Span<byte> span) where T : class, IIl2CppObjectBase
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
}
