using Il2CppInterop.Common;

namespace Il2CppSystem;

public class Object : IObject
{
    protected Object()
    {
    }

    protected Object(ObjectPointer ptr)
    {
    }

    public nint Pointer => default;

    public bool WasCollected => default;

    public virtual Boolean Equals(IObject @object) => default;
    public virtual Int32 GetIl2CppHashCode() => default;
    public virtual void Il2CppFinalize()
    {
    }

    public virtual String ToIl2CppString() => default;
}
