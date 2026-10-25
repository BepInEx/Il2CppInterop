using Il2CppInterop.Common;

namespace Il2CppSystem;

public class Object : IObject, IIl2CppType<Object>
{
    protected Object()
    {
    }

    protected Object(ObjectPointer ptr)
    {
    }

    static int IIl2CppType<Object>.Size => throw new System.NotImplementedException();

    public nint Pointer => default;

    public bool WasCollected => default;

    nint IIl2CppType.ObjectClass => throw new System.NotImplementedException();

    static Object IIl2CppType<Object>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw new System.NotImplementedException();
    static void IIl2CppType<Object>.WriteToSpan(Object value, System.Span<byte> span) => throw new System.NotImplementedException();
    public virtual Boolean Equals(IObject @object) => default;
    public virtual Int32 GetIl2CppHashCode() => default;
    public virtual void Il2CppFinalize()
    {
    }

    public virtual String ToIl2CppString() => default;
    ObjectPointer IIl2CppType.BoxNative() => throw new System.NotImplementedException();
}
