using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Int16 : IIl2CppType<Int16>
{
    static int IIl2CppType<Int16>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Int16 IIl2CppType<Int16>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Int16>.WriteToSpan(Int16 value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator short(Int16 value) => throw null;
    public static implicit operator Int16(short value) => throw null;
}
