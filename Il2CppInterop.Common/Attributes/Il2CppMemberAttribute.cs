namespace Il2CppInterop.Common.Attributes;

public abstract class Il2CppMemberAttribute : Attribute
{
    public string? Name { get; init; }
    private protected Il2CppMemberAttribute()
    {
    }
}
