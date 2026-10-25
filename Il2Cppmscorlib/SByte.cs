using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct SByte : IIl2CppType<SByte>
{
    static int IIl2CppType<SByte>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static SByte IIl2CppType<SByte>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<SByte>.WriteToSpan(SByte value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator sbyte(SByte value) => throw null;
    public static implicit operator SByte(sbyte value) => throw null;
}
