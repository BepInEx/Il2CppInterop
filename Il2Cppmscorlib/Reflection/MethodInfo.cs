using Il2CppInterop.Runtime.InteropTypes;

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

    public MethodInfo(ObjectPointer pointer)
    {
    }

    public MethodInfo MakeGenericMethod(Type[] typeArguments)
    {
        throw null;
    }
}
