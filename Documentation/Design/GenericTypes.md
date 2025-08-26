# Generic Types

```cs
Il2CppClassPointerStore<Dictionary<TKey, TValue>>.NativeClassPtr = IL2CPP.il2cpp_class_from_type(Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(IL2CPP.GetIl2CppClass("mscorlib.dll", "System.Collections.Generic", "Dictionary`2"))).MakeGenericType(new Il2CppReferenceArray<Type>(new Type[2]
{
    Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore<TKey>.NativeClassPtr)),
    Type.internal_from_handle(IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore<TValue>.NativeClassPtr))
})).TypeHandle.value);
IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<Dictionary<TKey, TValue>>.NativeClassPtr);
```
