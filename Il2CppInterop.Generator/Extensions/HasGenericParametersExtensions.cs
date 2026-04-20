using System.Reflection;
using Cpp2IL.Core.Model.Contexts;
using LibCpp2IL.BinaryStructures;

namespace Il2CppInterop.Generator.Extensions;

internal static class HasGenericParametersExtensions
{
    public static void CopyGenericParameters(this HasGenericParameters destination, HasGenericParameters source, bool copyConstraints = false, bool clearVarianceAttributes = false)
    {
        var type = destination is TypeAnalysisContext ? Il2CppTypeEnum.IL2CPP_TYPE_VAR : Il2CppTypeEnum.IL2CPP_TYPE_MVAR;
        foreach (var genericParameter in source.GenericParameters)
        {
            var attributes = clearVarianceAttributes ? genericParameter.Attributes & ~GenericParameterAttributes.VarianceMask : genericParameter.Attributes;
            destination.GenericParameters.Add(new GenericParameterTypeAnalysisContext(genericParameter.Name, destination.GenericParameters.Count, type, attributes, destination));
        }
        if (copyConstraints)
        {
            destination.GenericParameters.CopyConstraintsFrom(source.GenericParameters);
        }
    }
}
