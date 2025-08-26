using System;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
public unsafe struct ByReference<T>(T* value) : IIl2CppValueType, IIl2CppByReference
    where T : IIl2CppValueType
{
    static ByReference()
    {
        // Todo: set Il2CppClassPointerStore<ByReference<T>>.NativeClassPtr
    }

    private T* _value = value;

    readonly int IIl2CppByReference.ReferenceSize => typeof(T).IsValueType ? default(T)!.Size : IntPtr.Size;

    readonly nint IIl2CppByReference.ReferenceObjectClass => Il2CppClassPointerStore<T>.NativeClassPtr;

    readonly int IIl2CppValueType.Size => IntPtr.Size;

    readonly nint IIl2CppValueType.ObjectClass => Il2CppClassPointerStore<ByReference<T>>.NativeClassPtr;

    public static implicit operator ByReference<T>(T* value) => new(value);
    public static implicit operator T*(ByReference<T> pointer) => pointer._value;

    public static ByReference<T> FromRef(ref T value)
    {
        return new ByReference<T>((T*)Unsafe.AsPointer(ref value));
    }

    public static ref T ToRef(ByReference<T> value)
    {
        return ref Unsafe.AsRef<T>(value._value);
    }

    readonly void IIl2CppByReference.WriteReferenceToSpan(Span<byte> span)
    {
        if (typeof(T).IsValueType)
        {
            if (_value is null)
            {
                span.Slice(0, default(T)!.Size).Clear();
            }
            else
            {
                (*_value).WriteToSpan(span);
            }
        }
        else
        {
            if (_value is null)
            {
                Il2CppTypeHelper.WritePointer(IntPtr.Zero, span);
            }
            else
            {
                Il2CppTypeHelper.WritePointer((*_value).Box(), span);
            }
        }
    }

    void IIl2CppByReference.ReadReferenceFromSpan(ReadOnlySpan<byte> span)
    {
        if (_value is null)
        {
            // Cannot read a reference into a null pointer
        }
        else if (typeof(T).IsValueType)
        {
            (*_value).ReadFromSpan(span);
        }
        else
        {
            var pointer = Il2CppTypeHelper.ReadPointer(span);
            *_value = pointer == IntPtr.Zero ? default : (T?)Il2CppObjectPool.Get(pointer);
        }
    }

    readonly void IIl2CppValueType.WriteToSpan(Span<byte> span) => Il2CppTypeHelper.WritePointer((IntPtr)_value, span);
    void IIl2CppValueType.ReadFromSpan(ReadOnlySpan<byte> span) => _value = (T*)Il2CppTypeHelper.ReadPointer(span);
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
