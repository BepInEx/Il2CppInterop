using System;

namespace Il2CppInterop.Runtime.Attributes;

/// <summary>
/// Indicates that the attributed property is associated with a property in the IL2CPP runtime.
/// </summary>
/// <remarks>
/// Marking the accessor methods with <see cref="Il2CppMethodAttribute"/> is optional.
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public sealed class Il2CppPropertyAttribute : Il2CppMemberAttribute
{
}
