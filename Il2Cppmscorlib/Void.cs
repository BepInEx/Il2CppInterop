using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Void : IIl2CppType<Void>
{
    static int IIl2CppType<Void>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Void IIl2CppType<Void>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Void>.WriteToSpan(Void value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();
}
