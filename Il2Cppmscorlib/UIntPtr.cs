using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct UIntPtr : IIl2CppType<UIntPtr>
{
    static int IIl2CppType<UIntPtr>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static UIntPtr IIl2CppType<UIntPtr>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<UIntPtr>.WriteToSpan(UIntPtr value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator nuint(UIntPtr value) => throw null;
    public static implicit operator UIntPtr(nuint value) => throw null;
}
