using System;

namespace Il2CppInterop.Runtime.Attributes;

[AttributeUsage(AttributeTargets.Method)]
public class Il2CppMethodAttribute : Attribute
{
    public long RVA { get; set; } // I'd use IntPtr, but considering it's an attribute, it'd be a bad idea

    // I think this would also be a cool place to store original (unobfuscated) method names, but it would make the generated assemblies much bigger.
}
