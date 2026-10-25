using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct UInt64 : IIl2CppType<UInt64>
{
    static int IIl2CppType<UInt64>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static UInt64 IIl2CppType<UInt64>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<UInt64>.WriteToSpan(UInt64 value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator ulong(UInt64 value) => throw null;
    public static implicit operator UInt64(ulong value) => throw null;
}
