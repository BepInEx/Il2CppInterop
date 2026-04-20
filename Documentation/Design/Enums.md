# Enums

Enums are converted to readonly structs.

```cs
// Original
public enum ElectricityType
{
    Off = 0,
    On = 1
}

// Converted
public struct ElectricityType : IObject, IValueType, IEnum, IComparable, IFormattable, IConvertible
{
    private readonly Int32 value__;

    public static readonly ElectricityType Off = (ElectricityType)0;
    public static readonly ElectricityType On = (ElectricityType)1;
}
```

## Generic constraint

`Il2CppSystem.Enum` should be replaced with `Il2CppSystem.IEnum` in generic constraints.

## Interfaces

Additional interfaces like `IEquatable<>`, `IEqualityOperators<,,>`, and `IBitwiseOperators<,,>` could be introduced for user convenience.
