using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Int32 : IIl2CppType<Int32>
{
    static int IIl2CppType<Int32>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Int32 IIl2CppType<Int32>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Int32>.WriteToSpan(Int32 value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator int(Int32 value) => throw null;
    public static implicit operator Int32(int value) => throw null;
}
