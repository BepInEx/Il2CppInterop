using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.Extensions;

internal static class ParameterDefinitionEx
{
    public static bool IsParamsArray(this Parameter self)
    {
        return self.ParameterType is SzArrayTypeSignature && (self.Definition?.CustomAttributes.Any(attribute =>
            attribute.Constructor?.DeclaringType?.FullName == typeof(ParamArrayAttribute).FullName) ?? false);
    }
}
