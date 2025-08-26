# Injection

## Class

```cs
// User code

[InjectInIl2Cpp(Assembly = "OptionalAssemblyName")] // By default, types are injected into Assembly-CSharp
public partial class MyMonoBehaviour : MonoBehaviour
{
    [Il2CppField]
    public static partial int staticField { get; set; }
    [Il2CppField]
    public partial bool instanceField { get; set; }

    [HideFromIl2Cpp] // This attribute is valid on types, methods, properties. Injected classes cannot have uninjected instance fields and static fields are ignored by default.
    public static void DoAnything(string s) // If this wasn't hidden, it would be invalid because string is not a valid Il2Cpp parameter type.
    {
    }
}

// Source generated

partial class MyMonoBehaviour
{
    static MyMonoBehaviour()
    {
        ClassInjector.Inject(typeof(MyMonoBehaviour));
    }

    // Required
    public MyMonoBehaviour(ObjectPointer ptr) : base(ptr)
    {
    }

    public static partial int staticField { get => Il2CppInternals.staticField.Get(); set => Il2CppInternals.staticField.Set(value); }
    public partial bool instanceField { get => Il2CppInternals.instanceField.Get(this); set => Il2CppInternals.instanceField.Set(this, value); }
}
file static class Il2CppInternals // If the injected class is generic, this will also be generic.
{
    internal static readonly Il2CppStaticField<int> staticField = new("staticField", GetClassPointer);
    internal static readonly Il2CppField<bool> instanceField = new("instanceField", GetClassPointer);
    private static IntPtr GetClassPointer() => Il2CppClassPointerStore<MyMonoBehaviour>.NativeClassPtr;
}
```

## Struct

```cs
// User code

[InjectInIl2Cpp]
public partial struct MyStruct<T>
{
    [Il2CppField]
    public static partial T staticField { get; set; } // Fine because the injected field is static
    // [Il2CppField] is optional for struct instance fields.
    public bool instanceField;
    // public T unconstrainedGenericField; // Error: Injected generic fields in structs must be constrained value types.
    // public Il2CppSystem.Collection.Generic.List<T> listField; // Fine
}

// Source generated

partial struct MyStruct<T>
{
    static MyStruct()
    {
        ClassInjector.Inject(typeof(MyStruct));
    }

    public static partial T staticField { get => Il2CppInternals.staticField.Get(); set => Il2CppInternals.staticField.Set(value); }
}
file static class Il2CppInternals<T>
{
    internal static readonly Il2CppStaticField<T> staticField = new("staticField", GetClassPointer);
    private static IntPtr GetClassPointer() => Il2CppClassPointerStore<MyStruct>.NativeClassPtr;
}
```
