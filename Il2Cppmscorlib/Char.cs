using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Char : IIl2CppType<Char>
{
    static int IIl2CppType<Char>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Char IIl2CppType<Char>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Char>.WriteToSpan(Char value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator char(Char value) => throw null;
    public static implicit operator Char(char value) => throw null;
}
