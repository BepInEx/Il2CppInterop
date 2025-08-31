# Injection

## Major Flaw

Current injection has a major flaw. It waits until the last minute to allocate a new type. This can cause problems with:

* Cyclical field types (two classes have a field on each other)
* Self-referential field type
* Injected type used in base type or interfaces

## Class

```cs
// User code

// This attribute is for source generation and has no runtime impact.
[InjectInIl2Cpp(Assembly = "OptionalAssemblyName")] // By default, types are injected into Assembly-CSharp
public partial class MyMonoBehaviour : MonoBehaviour
{
    [Il2CppField]
    public static partial Int32 staticField { get; set; }
    [Il2CppField]
    public partial Boolean instanceField { get; set; }

    // Note: Injected classes cannot have uninjected instance fields and static fields are always ignored.

    [Il2CppMethod]
    public static void DoSomething(String s)
    {
    }

    public static void DoAnything(string s) // If this wasn't hidden, it would be invalid because string is not a valid Il2Cpp parameter type.
    {
    }

    [Il2CppProperty]
    public static Int32 MyProperty { get => default; set {} }
}

// Source generated

partial class MyMonoBehaviour
{
    static MyMonoBehaviour()
    {
        ClassInjector.RegisterTypeInIl2Cpp<MyMonoBehaviour>();
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
    public static partial T staticField { get; set; }

    // [Il2CppField] is optional for struct instance fields.
    public bool instanceField;
}

// Source generated

partial struct MyStruct<T> : IIl2CppType<MyStruct<T>>
    where T : IIl2CppType<T>
{
    static MyStruct()
    {
        ClassInjector.RegisterTypeInIl2Cpp<MyStruct>());
    }

    public static partial T staticField { get => Il2CppInternals.staticField.Get(); set => Il2CppInternals.staticField.Set(value); }
}
file static class Il2CppInternals<T>
{
    internal static readonly Il2CppStaticField<T> staticField = new("staticField", GetClassPointer);
    private static IntPtr GetClassPointer() => Il2CppClassPointerStore<MyStruct>.NativeClassPtr;
}
```

## New Design

### Design Constraints

A class pointer needs registered in `Il2CppClassPointerStore` before anything else can reference this type. In other words, the class pointer needs registered before setting:

* Declaring type
* Nested types
* Base type
* Interface implementations
* Types in member signatures

In addition, we need to know the virtual table slot count when allocating data for the class because the virtual table is always positioned in method directly after the class struct.

Ideally, no Il2Cpp APIs should be called while class structs are partially initialized.
