using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Byte : IIl2CppType<Byte>
{
    static int IIl2CppType<Byte>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Byte IIl2CppType<Byte>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Byte>.WriteToSpan(Byte value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator byte(Byte value) => throw null;
    public static implicit operator Byte(byte value) => throw null;
}
