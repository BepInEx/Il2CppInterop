using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.Attributes;
using String = Il2CppSystem.String;
using Void = Il2CppSystem.Void;

namespace Il2CppInterop.Runtime;

public static class Il2CppClassPointerStore
{
    public static IntPtr GetNativeClassPointer(Type type)
    {
        if (type == typeof(void)) return Il2CppClassPointerStore<Void>.NativeClassPtr;
        if (type == typeof(String)) return Il2CppClassPointerStore<string>.NativeClassPtr;
        return (IntPtr)typeof(Il2CppClassPointerStore<>)
            .MakeGenericType(type)
            .GetField(nameof(Il2CppClassPointerStore<int>.NativeClassPtr))!
            .GetValue(null)!;
    }

    internal static void SetNativeClassPointer(Type type, IntPtr value)
    {
        typeof(Il2CppClassPointerStore<>)
            .MakeGenericType(type)
            .GetField(nameof(Il2CppClassPointerStore<int>.NativeClassPtr))!
            .SetValue(null, value);
    }
}

public static class Il2CppClassPointerStore<T>
{
    public static IntPtr NativeClassPtr;
    public static Type? CreatedTypeRedirect;

    static Il2CppClassPointerStore()
    {
        var targetType = typeof(T);
        if (!targetType.IsEnum)
        {
            RuntimeHelpers.RunClassConstructor(targetType.TypeHandle);
        }
        else
        {
            var assemblyName = targetType.Module.Name;
            var @namespace = targetType.Namespace;
            var name = targetType.Name;
            foreach (var customAttribute in targetType.CustomAttributes)
            {
                if (customAttribute.AttributeType != typeof(OriginalNameAttribute)) continue;
                assemblyName = (string?)customAttribute.ConstructorArguments[0].Value;
                @namespace = (string?)customAttribute.ConstructorArguments[1].Value;
                name = (string?)customAttribute.ConstructorArguments[2].Value;
            }
            assemblyName ??= "";
            @namespace ??= "";
            name ??= "";

            if (targetType.IsNested)
                NativeClassPtr =
                    IL2CPP.GetIl2CppNestedType(Il2CppClassPointerStore.GetNativeClassPointer(targetType.DeclaringType!),
                        name);
            else
                NativeClassPtr =
                    IL2CPP.GetIl2CppClass(assemblyName, @namespace, name);
        }

        if (targetType.IsPrimitive || targetType == typeof(string))
            RuntimeHelpers.RunClassConstructor(AppDomain.CurrentDomain.GetAssemblies()
                .Single(it => it.GetName().Name == "Il2Cppmscorlib").GetType("Il2Cpp" + targetType.FullName)!
                .TypeHandle);

        foreach (var customAttribute in targetType.CustomAttributes)
        {
            if (customAttribute.AttributeType != typeof(AlsoInitializeAttribute)) continue;

            var linkedType = (Type?)customAttribute.ConstructorArguments[0].Value;
            if (linkedType is not null)
                RuntimeHelpers.RunClassConstructor(linkedType.TypeHandle);
        }
    }
}
