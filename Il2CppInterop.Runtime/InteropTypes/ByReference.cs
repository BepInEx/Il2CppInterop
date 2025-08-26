using System;
using System.Runtime.CompilerServices;

namespace Il2CppInterop.Runtime.InteropTypes;

public unsafe struct ByReference<T>(void* pointer) : IIl2CppType<ByReference<T>>, IIl2CppByReference
    where T : IIl2CppType<T>
{
    static ByReference()
    {
        // Todo: set Il2CppClassPointerStore<ByReference<T>>.NativeClassPtr
    }

    private readonly void* _pointer = pointer;

    public readonly bool IsNull => _pointer is null;

    public readonly T? GetValue()
    {
        ThrowIfNull();
        return Il2CppTypeHelper.ReadFromPointer<T>(_pointer);
    }

    public readonly void SetValue(T? value)
    {
        ThrowIfNull();
        Il2CppTypeHelper.WriteToPointer(value, _pointer);
    }

    public readonly void* ToPointer() => _pointer;

    private static int ReferenceSize => T.Size;

    readonly int IIl2CppByReference.ReferenceSize => ReferenceSize;

    readonly nint IIl2CppByReference.ReferenceObjectClass => Il2CppClassPointerStore<T>.NativeClassPtr;

    static int IIl2CppType.Size => IntPtr.Size;

    readonly nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<ByReference<T>>.NativeClassPtr;

    public static explicit operator ByReference<T>(void* value) => new(value);
    public static explicit operator void*(ByReference<T> pointer) => pointer._pointer;

    private readonly void ThrowIfNull()
    {
        if (_pointer is null)
        {
            throw new NullReferenceException($"Cannot access reference of type {typeof(T).Name} because it is null.");
        }
    }

    public static ByReference<T> FromRef(ref T value)
    {
        return new ByReference<T>(Unsafe.AsPointer(ref value));
    }

    public static ref T ToRef(ByReference<T> value)
    {
        return ref Unsafe.AsRef<T>(value._pointer);
    }

    readonly void IIl2CppByReference.WriteReferenceToSpan(Span<byte> span)
    {
        ThrowIfNull();
        new ReadOnlySpan<byte>(_pointer, ReferenceSize).CopyTo(span);
    }

    readonly void IIl2CppByReference.ReadReferenceFromSpan(ReadOnlySpan<byte> span)
    {
        ThrowIfNull();
        span.Slice(0, ReferenceSize).CopyTo(new Span<byte>(_pointer, ReferenceSize));
    }

    static void IIl2CppType<ByReference<T>>.WriteToSpan(ByReference<T> value, Span<byte> span) => Il2CppTypeHelper.WritePointer((IntPtr)value._pointer, span);
    static ByReference<T> IIl2CppType<ByReference<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => (ByReference<T>)(void*)Il2CppTypeHelper.ReadPointer(span);
}
