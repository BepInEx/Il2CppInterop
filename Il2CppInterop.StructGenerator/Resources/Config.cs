using Il2CppInterop.StructGenerator.TypeGenerators;

namespace Il2CppInterop.StructGenerator.Resources;

internal static class Config
{
    // NOTE: Ignores are handled BEFORE renames
    public static readonly string[] ClassForcedIgnores =
    {
        // Ignore the reflection structs in object-internals.h
        "Il2CppPropertyInfo",
        "Il2CppMethodInfo"
    };

    public static readonly Dictionary<string, string> ClassRenames = new()
    {
        ["TypeInfo"] = "Il2CppClass",
        ["FieldInfo"] = "Il2CppFieldInfo",
        ["EventInfo"] = "Il2CppEventInfo",
        ["PropertyInfo"] = "Il2CppPropertyInfo",
        ["MethodInfo"] = "Il2CppMethodInfo"
    };

    public static readonly Dictionary<string, Type> ClassToGenerator = new()
    {
        ["Il2CppClass"] = typeof(Il2CppClassGenerator),
        ["Il2CppType"] = typeof(Il2CppTypeGenerator),
        ["Il2CppAssembly"] = typeof(Il2CppAssemblyGenerator),
        ["Il2CppAssemblyName"] = typeof(Il2CppAssemblyNameGenerator),
        ["Il2CppFieldInfo"] = typeof(Il2CppFieldInfoGenerator),
        ["Il2CppImage"] = typeof(Il2CppImageGenerator),
        ["Il2CppEventInfo"] = typeof(Il2CppEventInfoGenerator),
        ["Il2CppException"] = typeof(Il2CppExceptionGenerator),
        ["Il2CppPropertyInfo"] = typeof(Il2CppPropertyInfoGenerator),
        ["Il2CppMethodInfo"] = typeof(Il2CppMethodInfoGenerator)
    };

    public static readonly string[] MetadataVersionContainers =
    {
        Path.Combine("vm", "MetadataCache.cpp"),
        Path.Combine("vm", "GlobalMetadata.cpp")
    };
}
