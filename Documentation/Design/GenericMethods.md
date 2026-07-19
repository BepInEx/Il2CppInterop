# Generic Methods

## Pointers

Pointers are resolved as needed using Il2Cpp reflection. It's fine to do this initialization lazily because all generic type instances are known in advance.

```cs
private static class MethodInfoStoreGeneric_Aggregate<TSource, TAccumulate>
{
    internal static System.IntPtr Pointer = IL2CPP.il2cpp_method_get_from_reflection(IL2CPP.Il2CppObjectBaseToPtrNotNull(new MethodInfo(IL2CPP.il2cpp_method_get_object(NativeMethodInfoPtr_Aggregate, Il2CppClassPointerStore<Enumerable>.NativeClassPtr))
    .MakeGenericMethod(new Il2CppReferenceArray<Type>(new Type[2]
    {
        Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore<TSource>.NativeClassPtr)),
        Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore<TAccumulate>.NativeClassPtr))
    }))));
}
```

If a modder tries to use a generic method instantiation that doesn't exist, an exception is thrown.

* <https://github.com/dreamanlan/il2cpp_ref/blob/09316fe508773b8ced098dae6147b44ee1f6516c/libil2cpp/icalls/mscorlib/System.Reflection/MonoMethod.cpp#L253>
* <https://github.com/dreamanlan/il2cpp_ref/blob/09316fe508773b8ced098dae6147b44ee1f6516c/libil2cpp/icalls/mscorlib/System.Reflection/MonoMethod.cpp#L305>
