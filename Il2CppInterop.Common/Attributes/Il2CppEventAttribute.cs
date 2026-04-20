namespace Il2CppInterop.Common.Attributes;

/// <summary>
/// Indicates that the attributed method is associated with an event in the IL2CPP runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class Il2CppEventAttribute : Il2CppMemberAttribute
{
}
