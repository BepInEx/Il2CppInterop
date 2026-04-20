# Value Types

## `readonly`

Structs that were originally readonly no longer are.

## `IValueType`

All value types implement this interface. It replaces `ValueType` anywhere that would normally be used, like in generic constraints.
