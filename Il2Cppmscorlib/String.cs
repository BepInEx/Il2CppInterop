using Il2CppInterop.Common;

namespace Il2CppSystem;

public sealed class String : Object
{
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
