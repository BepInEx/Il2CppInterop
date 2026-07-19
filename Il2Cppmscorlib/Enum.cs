using Il2CppInterop.Common;

namespace Il2CppSystem;

public abstract class Enum : ValueType, IIl2CppType<Enum>
{
    static int IIl2CppType<Enum>.Size => throw new System.NotImplementedException();

    static Enum IIl2CppType<Enum>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw new System.NotImplementedException();
    static void IIl2CppType<Enum>.WriteToSpan(Enum value, System.Span<byte> span) => throw new System.NotImplementedException();
}
