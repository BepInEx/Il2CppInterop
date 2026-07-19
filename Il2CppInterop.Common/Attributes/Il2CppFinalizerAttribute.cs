namespace Il2CppInterop.Common.Attributes;

/// <summary>
/// Indicates that the attributed method should be called in the source generated finalizer for an injected class.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class Il2CppFinalizerAttribute : Attribute
{
}
