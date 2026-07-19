# Core Library Types

## Numeric Primitives, `char`, and `bool`

These are blittable and should be directly reused, kind of. Signatures will use the Il2Cpp types, but implicit conversions will be used inside unstripped method bodies.

```cs
public static Il2CppSystem.Int32 Add(Il2CppSystem.Int32 x, Il2CppSystem.Int32 y)
{
    // Conversions to Managed are applied on function entry
    ref 
    int x2 = (int)x;
    int y2 = (int)y;

    // All operations are done with managed types
    int z = x2 + y2;

    // If an Il2Cpp primitive must be returned, conversion back is applied right before the return instruction.
    return (Il2CppSystem.Int32)z;
}
```

This ensures that CIL opcodes function as expected. However, it can cause some complexities with instance methods on the corlib types. For that, unsafe helpers are used.

```cs
public static Il2CppSystem.String Sum(Il2CppSystem.Int32 x, Il2CppSystem.Int32 y)
{
    int z = (int)x + (int)y;
    return Unsafe.As<int, Il2CppSystem.Int32>(ref z).ToString();
}
```

## `string`

Strings should not be implicitly marshalled. In other words, `Il2CppSystem.String` should be used.

## `object`

For compatibility with interfaces and value types, this should be emitted as-is, despite the more complicated marshalling involved.

## `Attribute`

## `ValueType` and `Enum`

Boxing to these types is invalid.

## `Exception`

## Counterargument to all of the above

```cs
// Original code
public static void DoSomething<T>(T value) where T : System.IConvertible
{
}
public static void DoSomethingElse()
{
    DoSomething<System.Enum>(System.StringComparison.Ordinal);
    DoSomething<System.StringComparison>(default);
    DoSomething<int>(default);
}

// Unstripped code
public static void DoSomething<T>() where T : Il2CppSystem.IConvertible
{
}
public static void DoSomethingElse()
{
    // Which is correct?
    // System.Enum fails the constraint check.
    // Il2CppSystem.Enum makes the method unusable because boxed enums inherit from `System.Enum` not `Il2CppSystem.Enum`.
    DoSomething<System.Enum>(Il2CppSystem.StringComparison.Ordinal);
    DoSomething<Il2CppSystem.Enum>(Il2CppSystem.StringComparison.Ordinal);
    DoSomething<Il2CppSystem.Enum>(Cast(Il2CppSystem.StringComparison.Ordinal)); // Maybe this is the way it should be emitted?

    // If the type is a real enum, it fails the constraint check.
    DoSomething<Il2CppSystem.StringComparison>(default);
    
    // Which is correct?
    // int fails the constraint check, but is what we currently do.
    DoSomething<int>(default);
    DoSomething<Il2CppSystem.Int32>(default);
}
private static Il2CppSystem.Enum Cast(object value)
{
   if (value is Il2CppSystem.Enum il2cppEnum)
    {
        return il2cppEnum;
    }
    if (value is System.Enum sysEnum)
    {
        throw new NotImplementedException("Cannot cast System.Enum to Il2CppSystem.Enum");
    }
    throw new InvalidCastException("Cannot cast to Il2CppSystem.Enum");
}

// Proposal
public static void DoSomething<T>() where T : Il2CppSystem.IConvertible
{
}
public static void DoSomethingElse()
{
    DoSomething<Il2CppSystem.IEnum>(Il2CppSystem.StringComparison.Ordinal);
    DoSomething<Il2CppSystem.StringComparison>(default);
    DoSomething<Il2CppSystem.Int32>(default);
}
namespace Il2CppSystem
{
    public interface IObject
    {
        // Instance members of Il2CppSystem.Object
    }
    public interface IValueType : IObject
    {
        // No members
    }
    public interface IEnum : IValueType, Il2CppSystem.IComparable, Il2CppSystem.IFormattable, Il2CppSystem.IConvertible
    {
        // Instance members of Il2CppSystem.Enum, except for interface implementations
    }
    public class Object : IObject
    {
        // A static method should be generated for each instance method
    }
    public abstract class ValueType : Object, IValueType
    {
        // A static method should be generated for each instance method
    }
    public abstract class Enum : ValueType, IEnum
    {
        // A static method should be generated for each instance method
    }
    public readonly struct StringComparison : IEnum // Maybe inject other interfaces like System.IEquatable<> for user convenience
    {
        // [System.Flags] // Only if the Il2Cpp enum has the Flags attribute
        private enum __Internal
        {
            CurrentCulture = 0,
            CurrentCultureIgnoreCase = 1,
            InvariantCulture = 2,
            InvariantCultureIgnoreCase = 3,
            Ordinal = 4,
            OrdinalIgnoreCase = 5
        }

        // Might make this `int` instead. The only reason to use `__Internal` is to have a more efficient ToString implementation.
        private readonly __Internal value__;

        // Sacrifice the ability to use Il2Cpp enums in constants.
        public static readonly StringComparison CurrentCulture = new StringComparison(__Internal.CurrentCulture);
        public static readonly StringComparison CurrentCultureIgnoreCase = new StringComparison(__Internal.CurrentCultureIgnoreCase);
        public static readonly StringComparison InvariantCulture = new StringComparison(__Internal.InvariantCulture);
        public static readonly StringComparison InvariantCultureIgnoreCase = new StringComparison(__Internal.InvariantCultureIgnoreCase);
        public static readonly StringComparison Ordinal = new StringComparison(__Internal.Ordinal);
        public static readonly StringComparison OrdinalIgnoreCase = new StringComparison(__Internal.OrdinalIgnoreCase);

        private StringComparison(__Internal value) => value__ = value;
        public StringComparison(int value) => value__ = unchecked((__Internal)value);
        public static explicit operator int(StringComparison value) => unchecked((int)value.value__);
        public static explicit operator StringComparison(int value) => new StringComparison(value);

        // Numerical operators like shift

        // Override ToString, GetHashCode, Equals, etc.
        public override int GetHashCode()
        {
            // Use the static method from Il2CppSystem.Enum
            // We need to ensure that behavior is consistent with the native method.
            return Il2CppSystem.Enum.GetHashCode(this);
        }

        static StringComparison()
        {
            // OriginalNameAttribute no longer needed.
        }
    }
    public interface ICloneable : IObject, System.ICloneable
    {
        IObject Clone();

        object System.ICloneable.Clone()
        {
            return Clone();
        }
    }
}
```
