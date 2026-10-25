# Fields

## Generated code

```cs
// Reference type
public FieldType fieldName
{
    get => throw null;
    set => throw null;
}
public static ByReference<FieldType> GetFieldAddress_fieldName(Class obj) => throw null;
private static readonly IntPtr FieldInfoPtr_fieldIndex;

// Value type
public FieldType fieldName;
public static ByReference<FieldType> GetFieldAddress_fieldName(ByReference<Struct> obj) => throw null;
private static readonly IntPtr FieldInfoPtr_fieldIndex;

// Static field
public static FieldType staticFieldName
{
    get => throw null;
    set => throw null;
}
public static ByReference<FieldType> GetFieldAddress_staticFieldName() => throw null;
private static readonly IntPtr FieldInfoPtr_fieldIndex;
```

## Field addresses

The address accessors translate `ldflda` and `ldsflda` in unstripped method bodies, such as the accessors of events, which call `Interlocked.CompareExchange` on the event's field.

A static field's address is in the static field data of its class, which `il2cpp_class_get_static_field_data` returns. Il2Cpp exports it from Unity 2021, so static address accessors are only generated for games that export it. Getting the address runs the class's static constructor first, as any access to a static field does. Thread static fields have no address there.
