# Return Objects

## Value Types

These should be returned "as-is".

```cs
return *(bool*)IL2CPP.il2cpp_object_unbox(intPtr);
```

## Sealed Classes

Since these can't be inherited from, we can call the pointer constructor directly. However, we may want to treat all classes the same, for simplicity.

## Normal Classes

During initialization, we cache delegates (or function pointers) for creating an object.

```cs
private static object Create(ObjectPointer ptr) => new Class(ptr);
internal static void Initialize()
{
    Il2CppObjectPool.RegisterFactoryMethod(Il2CppClassPointerStore<Class>.NativeClassPtr, (Func<ObjectPointer, object>)Create);
}
```

When returning from a method, we check the pool to see if the object already exists. If the object doesn't yet exist in managed code, we use the cached factory method to create a new one.

```cs
// Maybe instead Il2CppObjectPool.Get should perform this null check?
return (intPtr != (System.IntPtr)0) ? (ReturnType)Il2CppObjectPool.Get(intPtr) : null;
```

In the event that a factory method has not been registered, we throw an exception. If we pre-register factory methods for all generic type instances this should never happen.
