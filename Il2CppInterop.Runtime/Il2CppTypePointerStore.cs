using System.Diagnostics.CodeAnalysis;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Structs;

namespace Il2CppInterop.Runtime;

internal static class Il2CppTypePointerStore
{
    [RequiresDynamicCode("")]
    public static nint GetNativeTypePointer(System.Type type)
    {
        if (type == typeof(void))
            return Il2CppTypePointerStore<Il2CppSystem.Void>.NativeTypePointer;

        return (nint)typeof(Il2CppTypePointerStore<>)
            .MakeGenericType(type)
            .GetProperty(nameof(Il2CppTypePointerStore<>.NativeTypePointer))!
            .GetValue(null)!;
    }
}

internal static unsafe class Il2CppTypePointerStore<T> where T : IIl2CppType<T>
{
    public static nint NativeTypePointer
    {
        [RequiresDynamicCode("")]
        get
        {
            if (field == default)
            {
                var classPointer = Il2CppType.GetClassPointer<T>();
                if (classPointer != default)
                {
                    field = IL2CPP.il2cpp_class_get_type(classPointer);
                }
                else if (typeof(IByReference).IsAssignableFrom(typeof(T)))
                {
                    // For ByReference<T>, we can still get the type pointer even if T isn't an Il2Cpp type, as long as T is a valid ByReference element type
                    var elementType = typeof(T).GetGenericArguments()[0];
                    var elementTypePointer = Il2CppTypePointerStore.GetNativeTypePointer(elementType);
                    if (elementTypePointer != default)
                    {
                        var elemType = UnityVersionHandler.Wrap((Il2CppTypeStruct*)elementTypePointer);
                        var refType = UnityVersionHandler.NewType();
                        refType.Data = elemType.Data;
                        refType.Attrs = elemType.Attrs;
                        refType.Type = elemType.Type;
                        refType.ByRef = true;
                        refType.Pinned = elemType.Pinned;
                        field = refType.Pointer;
                    }
                }
            }
            return field;
        }
    }
}
