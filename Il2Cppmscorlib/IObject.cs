using Il2CppInterop.Common;

namespace Il2CppSystem;

public interface IObject : IIl2CppType<IObject>
{
    Boolean Equals(IObject @object) => default;
    Int32 GetIl2CppHashCode() => default;
    void Il2CppFinalize()
    {
    }

    String ToIl2CppString() => default;
}
