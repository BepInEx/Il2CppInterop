using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Single : IIl2CppType<Single>
{
    static int IIl2CppType<Single>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Single IIl2CppType<Single>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Single>.WriteToSpan(Single value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator float(Single value) => throw null;
    public static implicit operator Single(float value) => throw null;
}
