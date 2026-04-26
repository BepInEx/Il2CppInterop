using System.Runtime.CompilerServices;
using Il2CppSystem;

namespace Il2CppInterop.Runtime;

public static class Il2CppClassPointerStore
{
    public static nint GetNativeClassPointer(System.Type type)
    {
        if (type == typeof(void))
            return Il2CppClassPointerStore<Void>.NativeClassPointer;

        return (nint)typeof(Il2CppClassPointerStore<>)
            .MakeGenericType(type)
            .GetField(nameof(Il2CppClassPointerStore<>.NativeClassPointer))!
            .GetValue(null)!;
    }

    internal static void SetNativeClassPointer(System.Type type, nint value)
    {
        typeof(Il2CppClassPointerStore<>)
            .MakeGenericType(type)
            .GetField(nameof(Il2CppClassPointerStore<>.NativeClassPointer))!
            .SetValue(null, value);
    }
}

public static class Il2CppClassPointerStore<T>
{
    public static nint NativeClassPointer;

    static Il2CppClassPointerStore()
    {
        if (typeof(T) == typeof(IObject))
        {
            NativeClassPointer = Il2CppClassPointerStore<Object>.NativeClassPointer;
        }
        else if (typeof(T) == typeof(IValueType))
        {
            NativeClassPointer = Il2CppClassPointerStore<ValueType>.NativeClassPointer;
        }
        else if (typeof(T) == typeof(IEnum))
        {
            NativeClassPointer = Il2CppClassPointerStore<Enum>.NativeClassPointer;
        }
        else
        {
            RuntimeHelpers.RunClassConstructor(typeof(T).TypeHandle);
        }
    }
}
