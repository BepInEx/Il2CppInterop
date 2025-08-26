# Forbidden Types

Some types are poison and cannot be used anywhere.

* Arrays of pointers
  * Pointer types can't be used as generic arguments
* Static classes
  * Exception for stuff like `typeof()` and member references
* Pointer and byref types for nonblittable base types
  * The C# representation of these is very different from Il2Cpp.
* Any type specification that uses the above

## Pointers

These could also be made generic.

```cs
public readonly unsafe struct Pointer<T>
{
    private readonly T* value;

    public Pointer(T* value) => this.value = value;
    public static implicit operator Pointer<T>(T* value) => new(value);
    public static implicit operator T*(Pointer<T> pointer) => pointer.value;
}
```

`void*` would become `Pointer<Il2CppSystem.Void>`.
