# Simple Classes

This document outlines the high level emission of simple classes.

## Universal Base Type

All reference classes inherit from `Il2CppObjectBase`.

## Constructors

All reference classes have an injected primary constructor. This carries the object pointer from derived classes to their base

```cs
public class Derived(ObjectPointer ptr) : MonoBehaviour(ptr)
{
    static Derived()
    {
        Il2CppClassPointerStore<Derived>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "Derived");
        IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<Derived>.NativeClassPtr);
        NativeMethodInfoPtr__ctor_Public_Void_0 = IL2CPP.GetIl2CppMethodByToken(Il2CppClassPointerStore<Derived>.NativeClassPtr, 100666213);
    }

    public unsafe Derived() : this(IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<Derived>.NativeClassPtr))
    {
        Unsafe.SkipInit(out IntPtr intPtr2);
        IntPtr intPtr = IL2CPP.il2cpp_runtime_invoke(NativeMethodInfoPtr__ctor_Public_Void_0, IL2CPP.Il2CppObjectBaseToPtrNotNull(this), (void**)null, ref intPtr2);
        Il2CppException.RaiseExceptionIfNecessary(intPtr2);
    }
}
```

A wrapper struct in the common library prevents conflicts.

```cs
public readonly record struct ObjectPointer(IntPtr Value)
{
    public static implicit operator ObjectPointer(IntPtr value) => new(value);
    public static implicit operator IntPtr(ObjectPointer value) => value.Value;
}
```

## Static classes

### Option 1: Make class abstract but not sealed and inject a private constructor

```cs
public abstract class StaticClass
{
    static StaticClass()
    {
        Il2CppClassPointerStore<StaticClass>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "StaticClass");
        IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<StaticClass>.NativeClassPtr);
    }

    private StaticClass()
    {
    }
}
```

### Option 2: Inject a nested class to use as a type parameter

```cs
public static class StaticClass
{
    static StaticClass()
    {
        Il2CppClassPointerStore<InjectedNestedClass>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "StaticClass");
        IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<InjectedNestedClass>.NativeClassPtr);
    }

    private sealed class InjectedNestedClass()
    {
    }
}
```

### Comparison

Option 1 is more friendly to the rest of the generation process, but option 2 is more representative of the actual situation and allows extension methods to function as intended.

Recommendation: option 1.
