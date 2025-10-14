using Il2CppInterop.Common;

namespace Il2CppSystem;

public class Object : IObject
{
    public Object()
    {
    }

    public Object(ObjectPointer ptr)
    {
    }

    public nint Pointer => default;

    public bool WasCollected => default;
}
