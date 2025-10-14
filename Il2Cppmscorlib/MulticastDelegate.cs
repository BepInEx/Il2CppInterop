using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppSystem;

public abstract class MulticastDelegate : Delegate
{
    public MulticastDelegate(ObjectPointer ptr) : base(ptr)
    {
    }
}
