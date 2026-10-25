using Il2CppInterop.Common;

namespace Il2CppSystem;

public struct Double : IIl2CppType<Double>
{
    static int IIl2CppType<Double>.Size => throw null;
    readonly nint IIl2CppType.ObjectClass => throw null;
    static Double IIl2CppType<Double>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Double>.WriteToSpan(Double value, System.Span<byte> span) => throw null;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();

    public static implicit operator double(Double value) => throw null;
    public static implicit operator Double(double value) => throw null;
}
