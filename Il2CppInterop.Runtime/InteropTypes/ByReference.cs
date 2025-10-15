using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public static class ByReference
{
    public static T? GetValue<T>(ByReference<T> byReference)
        where T : IIl2CppType<T>
    {
        return byReference.GetValue();
    }

    public static void SetValue1<T>(ByReference<T> byReference, T? value)
        where T : IIl2CppType<T>
    {
        byReference.SetValue(value);
    }

    public static void SetValue2<T>(T? value, ByReference<T> byReference)
        where T : IIl2CppType<T>
    {
        byReference.SetValue(value);
    }

    // ldflda
    public static unsafe ByReference<U> GetReferenceAtOffset<T, U>(ByReference<T> byReference, int offset)
        where T : IIl2CppType<T>
        where U : IIl2CppType<U>
    {
        return new ByReference<U>((byte*)byReference.ToPointer() + offset);
    }
}
public unsafe struct ByReference<T>(void* pointer) : IIl2CppType<ByReference<T>>
    where T : IIl2CppType<T>
{
    static ByReference()
    {
        var elementClassPtr = Il2CppClassPointerStore<T>.NativeClassPtr;
        var elementTypePtr = IL2CPP.il2cpp_class_get_type(elementClassPtr);
        var elementTypeObj = Il2CppSystem.Type.internal_from_handle(elementTypePtr);
        var byRefTypeObj = elementTypeObj.MakeByRefType();
        var byRefClassPtr = IL2CPP.il2cpp_class_from_type(byRefTypeObj.TypeHandle.value);
        Il2CppClassPointerStore<ByReference<T>>.NativeClassPtr = byRefClassPtr;
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

    public readonly void Clear()
    {
        ThrowIfNull();
        Span<byte> span = new(_pointer, ReferenceSize);
        span.Clear();
    }

    public readonly void CopyFrom(in T? value) => SetValue(value);

    public readonly void CopyTo(out T? value) => value = GetValue();

    public readonly void* ToPointer() => _pointer;

    private static int ReferenceSize => T.Size;

    readonly nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<ByReference<T>>.NativeClassPtr;

    static int IIl2CppType<ByReference<T>>.Size => IntPtr.Size;

    public static explicit operator ByReference<T>(void* value) => new(value);
    public static explicit operator void*(ByReference<T> pointer) => pointer._pointer;

    public static explicit operator ByReference<T>(IntPtr value) => new(value.ToPointer());
    public static explicit operator IntPtr(ByReference<T> pointer) => new(pointer._pointer);

    public Span<byte> AsSpan()
    {
        return new Span<byte>(_pointer, T.Size);
    }

    private readonly void ThrowIfNull()
    {
        if (_pointer is null)
        {
            throw new NullReferenceException($"Cannot access reference of type {typeof(T).Name} because it is null.");
        }
    }

    static void IIl2CppType<ByReference<T>>.WriteToSpan(ByReference<T> value, Span<byte> span) => Il2CppTypeHelper.WritePointer((IntPtr)value._pointer, span);
    static ByReference<T> IIl2CppType<ByReference<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => (ByReference<T>)(void*)Il2CppTypeHelper.ReadPointer(span);
}
