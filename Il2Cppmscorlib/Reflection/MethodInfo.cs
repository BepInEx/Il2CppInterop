namespace Il2CppSystem.Reflection;

public abstract class MethodInfo : MethodBase
{
    public virtual Type ReturnType
    {
        get
        {
            throw null;
        }
    }

    public MethodInfo MakeGenericMethod(Type[] typeArguments)
    {
        throw null;
    }
}
