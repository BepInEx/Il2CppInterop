using System.Runtime.CompilerServices;
using Il2CppSystem;

namespace Il2CppInterop.Runtime;

public static class Il2CppClassPointerStore
{
    public static nint GetNativeClassPointer(System.Type type)
    {
        if (type == typeof(void))
            return Il2CppClassPointerStore<Void>.NativeClassPtr;

        return (nint)typeof(Il2CppClassPointerStore<>)
            .MakeGenericType(type)
            .GetField(nameof(Il2CppClassPointerStore<>.NativeClassPtr))!
            .GetValue(null)!;
    }

    internal static void SetNativeClassPointer(System.Type type, nint value)
    {
        typeof(Il2CppClassPointerStore<>)
            .MakeGenericType(type)
            .GetField(nameof(Il2CppClassPointerStore<>.NativeClassPtr))!
            .SetValue(null, value);
    }
}

public static class Il2CppClassPointerStore<T>
{
    public static nint NativeClassPtr;

    static Il2CppClassPointerStore()
    {
        if (typeof(T) == typeof(IObject))
        {
            NativeClassPtr = Il2CppClassPointerStore<Object>.NativeClassPtr;
        }
        else if (typeof(T) == typeof(IValueType))
        {
            NativeClassPtr = Il2CppClassPointerStore<ValueType>.NativeClassPtr;
        }
        else if (typeof(T) == typeof(IEnum))
        {
            NativeClassPtr = Il2CppClassPointerStore<Enum>.NativeClassPtr;
        }
        else
        {
            RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        }
    }
}
