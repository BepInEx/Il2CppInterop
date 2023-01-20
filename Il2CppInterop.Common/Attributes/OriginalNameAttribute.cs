namespace Il2CppInterop.Common.Attributes;

// This attribute is only applied to enums so the class pointer
// can be resolved correctly at runtime
// https://github.com/BepInEx/Il2CppInterop/issues/66
[AttributeUsage(AttributeTargets.Enum, Inherited = false)]
public class OriginalNameAttribute : Attribute
{
    public readonly string AssemblyName;
    public readonly string Namespace;
    public readonly string Name;

    public OriginalNameAttribute(string assemblyName, string @namespace, string name)
    {
        AssemblyName = assemblyName;
        Namespace = @namespace;
        Name = name;
    }
}
