using Il2CppInterop.Common;

namespace Il2CppSystem;

public sealed class String : Object, IIl2CppType<String>
{
    static int IIl2CppType<String>.Size => throw null;
    nint IIl2CppType.ObjectClass => throw null;
    static String IIl2CppType<String>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<String>.WriteToSpan(String value, System.Span<byte> span) => throw null;

    public String(ObjectPointer pointer) : base(pointer)
    {
    }
    public static implicit operator String(string str)
    {
        throw null;
    }
    public static implicit operator string(String str)
    {
        throw null;
    }
}
