using Mono.Cecil;

namespace Il2CppInterop.Generator.Utils;

internal static class Cpp2ILUtil
{
    private static readonly string[] s_cpp2ilNamespaces =
    {
        "Cpp2ILInjected",
        "Cpp2IlInjected"
    };

    public static bool IsCpp2ILType(TypeDefinition type)
    {
        if (string.IsNullOrEmpty(type.Namespace))
            return false;

        return s_cpp2ilNamespaces.Contains(type.Namespace);
    }
}
