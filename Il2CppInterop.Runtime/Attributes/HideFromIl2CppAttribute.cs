using System;

namespace Il2CppInterop.Runtime.Attributes;

/// <summary>
///     This attribute indicates that the target should not be exposed to IL2CPP in injected classes
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property |
                AttributeTargets.Event | AttributeTargets.Field)]
public class HideFromIl2CppAttribute : Attribute
{
}
