using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal static class HasGenericParametersExtensions
{
    public static void CopyGenericParameters(this HasGenericParameters destination, HasGenericParameters source)
    {
        foreach (var genericParameter in source.GenericParameters)
        {
            destination.GenericParameters.Add(new GenericParameterTypeAnalysisContext(genericParameter.Name, genericParameter.Index, genericParameter.Type, genericParameter.Attributes, destination));
        }
    }
}
