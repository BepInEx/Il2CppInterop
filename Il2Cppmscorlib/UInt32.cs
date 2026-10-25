using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct UInt32 : IIl2CppType<UInt32>
{
    static int IIl2CppType<UInt32>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static UInt32 IIl2CppType<UInt32>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<UInt32>.WriteToSpan(UInt32 value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator uint(UInt32 value) => throw null;
    public static implicit operator UInt32(uint value) => throw null;
}
