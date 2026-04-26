using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct IntPtr : IIl2CppType<IntPtr>
{
    static int IIl2CppType<IntPtr>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static IntPtr IIl2CppType<IntPtr>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<IntPtr>.WriteToSpan(IntPtr value, System.Span<byte> span) => throw null;

    public static implicit operator nint(IntPtr value) => throw null;
    public static implicit operator IntPtr(nint value) => throw null;
}
