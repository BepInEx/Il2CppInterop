namespace Il2CppSystem.Reflection;

public abstract class MemberInfo : Object
{
    public virtual String Name { get; }

    public virtual Type DeclaringType { get; }
}
