using System;

namespace Il2CppInterop.Runtime.Attributes;

/// <summary>
/// Indicates that the attributed method is associated with a method in the IL2CPP runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class Il2CppMethodAttribute : Il2CppMemberAttribute
{
}
