using System.Reflection;
using Il2CppInterop.Runtime.Attributes;

namespace Il2CppInterop.Runtime.Extensions;

public static class ReflectionEx
{
    public static Il2CppMethodAttribute? GetIl2CppInfo(this MethodInfo method)
    {
        return method.GetCustomAttribute<Il2CppMethodAttribute>();
    }
}
