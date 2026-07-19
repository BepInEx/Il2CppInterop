using System.Diagnostics.CodeAnalysis;
using CppAst;
using Il2CppInterop.StructGenerator.TypeGenerators;

namespace Il2CppInterop.StructGenerator;

internal static class Config
{
    /// <summary>
    /// NOTE: Ignores are handled BEFORE renames
    /// </summary>
    public static readonly string[] ClassForcedIgnores =
    [
        // Ignore the reflection structs
        "Il2CppPropertyInfo",
        "Il2CppMethodInfo"
    ];

    public static readonly Dictionary<string, string> ClassRenames = new()
    {
        ["TypeInfo"] = "Il2CppClass",
        ["FieldInfo"] = "Il2CppFieldInfo",
        ["EventInfo"] = "Il2CppEventInfo",
        ["ParameterInfo"] = "Il2CppParameterInfo",
        ["PropertyInfo"] = "Il2CppPropertyInfo",
        ["MethodInfo"] = "Il2CppMethodInfo"
    };

    public static readonly HashSet<string> ClassNames =
    [
        "Il2CppAssembly",
        "Il2CppAssemblyName",
        "Il2CppClass",
        "Il2CppEventInfo",
        "Il2CppException",
        "Il2CppFieldInfo",
        "Il2CppImage",
        "Il2CppMethodInfo",
        "Il2CppParameterInfo",
        "Il2CppPropertyInfo",
        "Il2CppType",
    ];

    public static VersionSpecificGenerator? TryCreateGenerator(CppClass nativeClass, string metadataSuffix) => nativeClass.Name switch
    {
        "Il2CppAssembly" => new Il2CppAssemblyGenerator(metadataSuffix, nativeClass),
        "Il2CppAssemblyName" => new Il2CppAssemblyNameGenerator(metadataSuffix, nativeClass),
        "Il2CppClass" => new Il2CppClassGenerator(metadataSuffix, nativeClass),
        "Il2CppEventInfo" => new Il2CppEventInfoGenerator(metadataSuffix, nativeClass),
        "Il2CppException" => new Il2CppExceptionGenerator(metadataSuffix, nativeClass),
        "Il2CppFieldInfo" => new Il2CppFieldInfoGenerator(metadataSuffix, nativeClass),
        "Il2CppImage" => new Il2CppImageGenerator(metadataSuffix, nativeClass),
        "Il2CppMethodInfo" => new Il2CppMethodInfoGenerator(metadataSuffix, nativeClass),
        "Il2CppParameterInfo" => new Il2CppParameterInfoGenerator(metadataSuffix, nativeClass),
        "Il2CppPropertyInfo" => new Il2CppPropertyInfoGenerator(metadataSuffix, nativeClass),
        "Il2CppType" => new Il2CppTypeGenerator(metadataSuffix, nativeClass),
        _ => null
    };

    public static bool TryCreateGenerator(CppClass nativeClass, string metadataSuffix, [NotNullWhen(true)] out VersionSpecificGenerator? generator)
    {
        generator = TryCreateGenerator(nativeClass, metadataSuffix);
        return generator != null;
    }
}
