using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Nullable<T> : IObject, IValueType, IIl2CppType, IIl2CppType<Nullable<T>>
    where T : struct, IValueType, IIl2CppType<T>, IObject
{
    public Boolean hasValue;

    public T value;

    static int IIl2CppType<Nullable<T>>.Size => throw new System.NotImplementedException();
    nint IIl2CppType.ObjectClass => throw new System.NotImplementedException();
    static Nullable<T> IIl2CppType<Nullable<T>>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw new System.NotImplementedException();
    static void IIl2CppType<Nullable<T>>.WriteToSpan(Nullable<T> value, System.Span<byte> span) => throw new System.NotImplementedException();
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();
}
