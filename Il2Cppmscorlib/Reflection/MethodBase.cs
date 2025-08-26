namespace Il2CppSystem.Reflection;

public abstract class MethodBase : MemberInfo
{
    public virtual ParameterInfo[] GetParameters() => [];
}
