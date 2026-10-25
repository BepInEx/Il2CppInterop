using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct UInt16 : IIl2CppType<UInt16>
{
    static int IIl2CppType<UInt16>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static UInt16 IIl2CppType<UInt16>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<UInt16>.WriteToSpan(UInt16 value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator ushort(UInt16 value) => throw null;
    public static implicit operator UInt16(ushort value) => throw null;
}
