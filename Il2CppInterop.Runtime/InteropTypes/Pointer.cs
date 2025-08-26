using System;

namespace Il2CppInterop.Runtime.InteropTypes;

#pragma warning disable CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
public unsafe struct Pointer<T>(T* value) : IIl2CppValueType
    where T : IIl2CppValueType
{
    static Pointer()
    {
        // Todo: set Il2CppClassPointerStore<Pointer<T>>.NativeClassPtr
    }

    private T* _value = value;

    readonly int IIl2CppValueType.Size => IntPtr.Size;

    readonly nint IIl2CppValueType.ObjectClass => Il2CppClassPointerStore<Pointer<T>>.NativeClassPtr;

    void IIl2CppValueType.ReadFromSpan(ReadOnlySpan<byte> span) => _value = (T*)Il2CppTypeHelper.ReadPointer(span);
    readonly void IIl2CppValueType.WriteToSpan(Span<byte> span) => Il2CppTypeHelper.WritePointer((IntPtr)_value, span);

    public static implicit operator Pointer<T>(T* value) => new(value);
    public static implicit operator T*(Pointer<T> pointer) => pointer._value;
}
#pragma warning restore CS8500 // This takes the address of, gets the size of, or declares a pointer to a managed type
