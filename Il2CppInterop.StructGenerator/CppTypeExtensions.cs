using CppAst;

namespace Il2CppInterop.StructGenerator;

internal static class CppTypeExtensions
{
    public static CppClass? AsClass(this CppType? type) => type switch
    {
        CppClass cppClass => cppClass,
        CppTypedef cppTypedef => cppTypedef.ElementType.AsClass(),
        _ => null
    };
}
