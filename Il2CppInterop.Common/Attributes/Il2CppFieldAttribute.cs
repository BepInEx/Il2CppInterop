namespace Il2CppInterop.Common.Attributes;

/// <summary>
/// Indicates that the attributed field is associated with a field in the IL2CPP runtime.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class Il2CppFieldAttribute : Il2CppMemberAttribute
{
}
