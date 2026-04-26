using System.Reflection;
using Il2CppInterop.Common.Attributes;

namespace Il2CppInterop.Common;

public static class Il2CppInteropUtils
{
    private static FieldInfo? GetFieldInfo(Type declaringType, string prefix, int index)
    {
        if (index < 0)
            return null;

        var internalsType = ResolveInternals(declaringType);
        if (internalsType == null)
            return null;

        return internalsType.GetField($"{prefix}{index}", BindingFlags.Static | BindingFlags.NonPublic);

        static Type? ResolveInternals(Type declaringType)
        {
            var attr = declaringType.GetCustomAttribute<Il2CppTypeAttribute>();
            if (attr == null)
                return null;

            var internals = attr.Internals;

            if (internals.IsGenericTypeDefinition && declaringType.IsConstructedGenericType)
            {
                internals = internals.MakeGenericType(declaringType.GetGenericArguments());
            }

            return internals;
        }
    }

    public static FieldInfo? GetIl2CppMethodInfoPointerFieldForGeneratedMethod(MethodBase method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType == null)
            return null;

        var index = method.GetCustomAttribute<Il2CppMethodAttribute>()?.Index ?? -1;

        return GetFieldInfo(declaringType, "MethodInfoPtr_", index);
    }

    public static FieldInfo? GetIl2CppFieldInfoPointerFieldForGeneratedFieldAccessor(MethodBase method)
    {
        var declaringType = method.DeclaringType;
        if (declaringType == null)
            return null;

        var prop = declaringType
            .GetProperties(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(p => p.GetMethod == method || p.SetMethod == method);

        var index = prop?.GetCustomAttribute<Il2CppFieldAttribute>()?.Index ?? -1;

        return GetFieldInfo(declaringType, "FieldInfoPtr_", index);
    }
}
