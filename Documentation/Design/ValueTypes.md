# Value Types

## `readonly`

Structs that were originally readonly no longer are.

## Non-generic with only value type fields

These can be passed to Il2Cpp directly.

## Non-generic with at least one reference type field

The reason these aren't blittable is because they contain reference types. Those field can be typed as `IntPtr` instead.

```cs
public struct StructWithReferenceField
{
    public bool boolField;
    private IntPtr monoBehaviourField_BackingField;
    public int intField;

    public MonoBehaviour monoBehaviourField
    {
        get => (MonoBehaviour)Il2CppObjectPool.Get(monoBehaviourField_BackingField);
        set => monoBehaviourField_BackingField = Il2CppObjectBase.GetPointer(value);
    }
}
```

## Constrained Generic

If a generic parameter is constrained to be a value type, no special handling is required.

If a generic parameter is constrained to be a reference type, fields with it as the type can be treated the same as non-generic reference type fields.

## Unconstrained Generic

These are the most tricky. Let's have a simple example:

```cs
public struct Pair<T>
{
    public T Item1;
    public T Item2;
}
```

### Idea 1: Add a second type parameter for the backing field

```cs
public struct Pair<T, T_Backing> where T_Backing : struct
{
    private T_Backing Item1_BackingField;
    private T_Backing Item2_BackingField;

    public T Item1 { get => throw null; set => throw null; }
    public T Item2 { get => throw null; set => throw null; }
}
```

However, that transformation has quite a few issues.

* How should the injected type parameter be propagated up?
  * `static void M<T>(Pair<T> pair) {}` -> `static void M<T, T_Backing>(Pair<T, T_Backing> pair) {}`
  * Finding everywhere that it gets used and changing them could be difficult.
* Should the IL name of `Pair'1` be changed to `Pair'2`?
  * What about potential conflicts?
* Will this cause multiple Il2Cpp initializations?
  * This can be solved easily, but it's something to be aware of.

### Idea 2: Generate constrained helper classes

```cs
public struct Pair<T>
{
    public T Item1;
    public T Item2;

    // No change needed when T is a value type

    public struct T_ReferenceType
    {
        private IntPtr Item1_BackingField;
        private IntPtr Item2_BackingField;

        public T Item1 { get => throw null; set => throw null; }
        public T Item2 { get => throw null; set => throw null; }
    }
}
```

This idea doesn't seem very promising.

### Idea 3: Custom Marshalling

```cs
public struct Pair<T>
{
    public T Item1;
    public T Item2;

    public static int GetIl2CppSize() => throw null;
    public static void WriteToSpan(Pair<T> value, Span<byte> span)
    {
        span.Clear();

        // Item 1
        if (/* T is reference type */)
        {
            Span<byte> span2 = span.Slice(Item1_fieldOffset);
            IntPtr ptr = Il2CppObjectBase.GetPointer((IIl2CppObjectBase)Item1);
            MemoryMarshal.Write(span2, in ptr);
        }
        else
        {
            Span<byte> span2 = span.Slice(Item1_fieldOffset);
            MemoryMarshal.Write(span2, in value.Item1);
        }

        // Item 2
        if (/* T is reference type */)
        {
            Span<byte> span2 = span.Slice(Item2_fieldOffset);
            IntPtr ptr = Il2CppObjectBase.GetPointer((IIl2CppObjectBase)Item2);
            MemoryMarshal.Write(span2, in ptr);
        }
        else
        {
            Span<byte> span2 = span.Slice(Item2_fieldOffset);
            MemoryMarshal.Write(span2, in value.Item2);
        }
    }

    public static Pair<T> ReadFromSpan(ReadOnlySpan<byte> span)
    {
        // Essentially the same as WriteToSpan
    }

    public static unsafe Pair<T> ReadFromPointer(IntPtr ptr)
    {
        return ReadFromSpan(new ReadOnlySpan<byte>((void*)ptr, GetIl2CppSize()));
    }
}
```

Also required for any value type that uses it, recursively.

```cs
public struct HasPair
{
    public Pair<int> pairField; // It doesn't matter that this is instantiated. The extra methods are still required.
    public int otherField;

    public static int GetIl2CppSize() => throw null;
    public static void WriteToSpan(HasPair value, Span<byte> span)
    {
        span.Clear();

        // pairField
        {
            Span<byte> span2 = span.Slice(pairField_fieldOffset, Pair<int>.GetIl2CppSize());
            MemoryMarshal.Write(span2, in value.pairField);
        }

        // otherField
        {
            Span<byte> span2 = span.Slice(otherField_fieldOffset);
            MemoryMarshal.Write(span2, in value.otherField);
        }
    }

    public static HasPair ReadFromSpan(ReadOnlySpan<byte> span)
    {
        // Essentially the same as WriteToSpan
    }

    public static unsafe HasPair ReadFromPointer(IntPtr ptr)
    {
        return ReadFromSpan(new ReadOnlySpan<byte>((void*)ptr, GetIl2CppSize()));
    }
}
```

In the case of a method returning one, it works like this:

```cs
return HasPair.ReadFromPointer(IL2CPP.il2cpp_object_unbox(intPtr));
```

Array access can potentially be handled with a subclass of a base array type.

Any method that uses one of these in their signature as a pointer (eg `HasPair*`) will throw a not supported exception. This includes generated properties for fields. Byref returns will throw, but parameters can be handled.

### Idea 4: Just throw

In theory, this could be treated as not supported, and we throw an exception anywhere they get used. It's not desirable, but it's a simple solution to the problem.

```cs
public struct Pair<T>
{
    public T Item1 { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public T Item2 { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
}
public struct HasPair
{
    public Pair<int> pairField { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public int otherField { get => throw new NotSupportedException(); set => throw new NotSupportedException(); } // One bad field poisons the rest in a struct
}
public class Class
{
    // Can't be accessed
    public Pair<int> pairField { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

    // This is fine
    public List<Pair<int>> pairListField { get => throw null; set => throw null; } // In a class, bad fields do not impact the others.

    public static void M1(Pair<int> pair) => throw new NotSupportedException();
    public static Pair<int> M2() => throw new NotSupportedException();
    public static void M3(HasPair pair) => throw new NotSupportedException();
    public static HasPair M4() => throw new NotSupportedException();
    public static void M5(List<Pair<int>> list)
    {
        // This is fine
    }
}
```

`ValueTuple` will probably be the biggest painpoint with this approach.

### Assessment

* Idea 1 seems prone to causing bugs.
* Idea 2 doesn't do anything for arrays.
* Idea 3 is complicated, but seems to be the best option.
* Idea 4 is simple and leaves the door open to future implementation.
