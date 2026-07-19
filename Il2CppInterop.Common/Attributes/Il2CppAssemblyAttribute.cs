namespace Il2CppInterop.Common.Attributes;

[AttributeUsage(AttributeTargets.Assembly)]
public sealed class Il2CppAssemblyAttribute : Attribute
{
    public string? Name { get; init; }
}
