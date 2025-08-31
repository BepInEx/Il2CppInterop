using System;

namespace Il2CppInterop.Runtime.Attributes;

public abstract class Il2CppMemberAttribute : Attribute
{
    public string? Name { get; set; }
    private protected Il2CppMemberAttribute()
    {
    }
}
