using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public unsafe struct Pointer<T>(void* pointer) : IIl2CppType<Pointer<T>>
    where T : IIl2CppType<T>
{
    static Pointer()
    {
        // Todo: set Il2CppClassPointerStore<Pointer<T>>.NativeClassPtr
    }

    private readonly void* _pointer = pointer;

    public readonly bool IsNull => _pointer is null;

    public readonly T? this[int index]
    {
        get
        {
            ThrowIfNull();
            void* start = (byte*)_pointer + T.Size * index;
            return Il2CppTypeHelper.ReadFromPointer<T>(start);
        }
        set
        {
            ThrowIfNull();
            void* start = (byte*)_pointer + T.Size * index;
            Il2CppTypeHelper.WriteToPointer(value, start);
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

    static int IIl2CppType.Size => IntPtr.Size;

    readonly nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<Pointer<T>>.NativeClassPtr;

    static Pointer<T> IIl2CppType<Pointer<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => (Pointer<T>)(void*)Il2CppTypeHelper.ReadPointer(span);
    static void IIl2CppType<Pointer<T>>.WriteToSpan(Pointer<T> value, Span<byte> span) => Il2CppTypeHelper.WritePointer((IntPtr)value._pointer, span);

    public static explicit operator Pointer<T>(void* value) => new(value);
    public static explicit operator void*(Pointer<T> pointer) => pointer._pointer;
}
