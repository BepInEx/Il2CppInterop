namespace Il2CppInterop.Common.Attributes;

/// <summary>
/// Indicates that the attributed partial property is an injected field with an uninjected reference type.
/// Code will be source generated to properly reference it using a managed GCHandle, which is freed when the object is collected by the Il2Cpp GC.
/// </summary>
/// <remarks>
/// Storing an Il2Cpp object in this field can cause memory leaks in some cases.
/// </remarks>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
public sealed class ManagedFieldAttribute : Attribute
{
    public string? Name { get; init; }
}
