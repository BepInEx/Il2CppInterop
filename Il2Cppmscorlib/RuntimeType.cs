using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public abstract class RuntimeType : TypeInfo
{
    public object GenericCache { get; set; }
}
