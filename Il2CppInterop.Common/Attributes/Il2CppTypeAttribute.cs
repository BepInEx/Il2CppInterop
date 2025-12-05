namespace Il2CppInterop.Common.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Struct)]
public sealed class Il2CppTypeAttribute(Type internals) : Attribute
{
    public Type Internals { get; } = internals;
}
