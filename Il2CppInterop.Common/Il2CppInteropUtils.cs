using System.Reflection;
using Il2CppInterop.Common.Attributes;

namespace Il2CppInterop.Common;

public static class Il2CppInteropUtils
{
    private static FieldInfo? GetFieldInfoFromMethod(MethodBase method, string prefix)
    {
        var declaringType = method.DeclaringType;
        if (declaringType == null)
            return null;

        Type? ResolveInternals(Type dt)
        {
            var attr = dt.GetCustomAttribute<Il2CppTypeAttribute>();
            if (attr == null)
                return null;

            var internals = attr.Internals;

            if (internals.IsGenericTypeDefinition && dt.IsConstructedGenericType)
            {
                internals = internals.MakeGenericType(dt.GetGenericArguments());
            }

            return internals;
        }

        var internalsType = ResolveInternals(declaringType);
        if (internalsType == null)
            return null;

        int index = method.GetCustomAttribute<Il2CppMethodAttribute>()?.Index ?? -1;

        if (index < 0)
        {
            var prop = declaringType
                .GetProperties(BindingFlags.Instance | BindingFlags.Static |
                               BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(p => p.GetMethod == method || p.SetMethod == method);

            if (prop != null)
                index = prop.GetCustomAttribute<Il2CppFieldAttribute>()?.Index ?? -1;
        }

        if (index < 0)
            return null;

        return internalsType.GetField(prefix + index,
            BindingFlags.Static | BindingFlags.NonPublic);
    }

    public static FieldInfo? GetIl2CppMethodInfoPointerFieldForGeneratedMethod(MethodBase method)
    {
        return GetFieldInfoFromMethod(method, "MethodInfoPtr_");
    }

    public static FieldInfo? GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(MethodBase method)
    {
        return GetFieldInfoFromMethod(method, "FieldInfoPtr_");
    }
}
