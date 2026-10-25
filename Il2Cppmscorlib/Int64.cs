using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Int64 : IIl2CppType<Int64>
{
    static int IIl2CppType<Int64>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Int64 IIl2CppType<Int64>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Int64>.WriteToSpan(Int64 value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator long(Int64 value) => throw null;
    public static implicit operator Int64(long value) => throw null;
}
