namespace Il2CppInterop.Common.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
public sealed class Il2CppTypeAttribute(Type internals) : Attribute
{
    public Type Internals { get; } = internals;
    /// <summary>
    /// The name of the type. If not specified, it will be inferred from the managed type's name.
    /// </summary>
    public string? Name { get; init; }
    /// <summary>
    /// The namespace of the type. If not specified, it will be inferred from the managed type's namespace.
    /// </summary>
    public string? Namespace { get; init; }
}
