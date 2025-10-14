using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public abstract class RuntimeType : TypeInfo
{
    public IObject GenericCache { get; set; }
}
