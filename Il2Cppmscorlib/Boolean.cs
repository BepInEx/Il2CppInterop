using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Boolean : IIl2CppType<Boolean>
{
    static int IIl2CppType<Boolean>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Boolean IIl2CppType<Boolean>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Boolean>.WriteToSpan(Boolean value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator bool(Boolean value) => throw null;
    public static implicit operator Boolean(bool value) => throw null;
}
