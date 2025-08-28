namespace Il2CppSystem.Reflection;

public class MethodInfo : MethodBase
{
    public virtual Type ReturnType
    {
        get
        {
            throw null;
        }
    }

    public MethodInfo(nint pointer)
    {
    }

    public MethodInfo MakeGenericMethod(Type[] typeArguments)
    {
        throw null;
    }
}
