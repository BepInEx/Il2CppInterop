using System;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Extensions;

namespace Il2CppInterop.Runtime.InteropTypes;

public readonly unsafe struct Pointer<T>(void* pointer) : IIl2CppType<Pointer<T>>, IPointer
    where T : IIl2CppType<T>
{
    private readonly void* _pointer = pointer;

    public static Pointer<T> Null => new(null);
    public readonly bool IsNull => _pointer is null;

    public readonly T? this[int index]
    {
        get
        {
            ThrowIfNull();
            void* start = (byte*)_pointer + T.Size * index;
            return Il2CppType.ReadFromPointer<T>(start);
        }
        set
        {
            ThrowIfNull();
            void* start = (byte*)_pointer + T.Size * index;
            Il2CppType.WriteToPointer(value, start);
        }
    }

    public readonly void* ToPointer() => _pointer;

    private readonly void ThrowIfNull()
    {
        if (_pointer is null)
        {
            throw new NullReferenceException($"Cannot access reference of type {typeof(T).Name} because it is null.");
        }
    }

    static int IIl2CppType<Pointer<T>>.Size => IntPtr.Size;

    readonly nint IIl2CppType.ObjectClass => Il2CppType.GetClassPointer<Pointer<T>>();

    static Pointer<T> IIl2CppType<Pointer<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => (Pointer<T>)(void*)Il2CppType.ReadPointer(span);
    static void IIl2CppType<Pointer<T>>.WriteToSpan(Pointer<T> value, Span<byte> span) => Il2CppType.WritePointer((IntPtr)value._pointer, span);
    public ObjectPointer BoxNative() => throw new NotSupportedException("Boxing is not supported for pointer types.");

    public static explicit operator Pointer<T>(void* value) => new(value);
    public static explicit operator void*(Pointer<T> pointer) => pointer._pointer;

    static Pointer()
    {
        var elementType = Il2CppSystem.Type.FromClassPointer(Il2CppType.GetClassPointer<T>());
        Il2CppType.SetClassPointer<Pointer<T>>(elementType.MakePointerType().ToClassPointer());
    }
}
