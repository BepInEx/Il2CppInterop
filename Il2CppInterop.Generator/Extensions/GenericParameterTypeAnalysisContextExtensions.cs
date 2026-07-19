using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Visitors;

namespace Il2CppInterop.Generator.Extensions;

internal static class GenericParameterTypeAnalysisContextExtensions
{
    public static void CopyConstraintsFrom(this GenericParameterTypeAnalysisContext destination, GenericParameterTypeAnalysisContext source, TypeReplacementVisitor visitor)
    {
        foreach (var constraint in source.ConstraintTypes)
        {
            destination.ConstraintTypes.Add(visitor.Replace(constraint));
        }
    }

    public static void CopyConstraintsFrom(this IReadOnlyList<GenericParameterTypeAnalysisContext> destination, IReadOnlyList<GenericParameterTypeAnalysisContext> source)
    {
        if (destination.Count != source.Count)
            throw new ArgumentException("Source and destination lists must have the same number of elements.");

        if (source.Count == 0)
            return;

        var replacements = new Dictionary<TypeAnalysisContext, TypeAnalysisContext>(source.Count);
        for (var i = source.Count - 1; i >= 0; i--)
        {
            replacements.Add(source[i], destination[i]);
        }

        var visitor = new TypeReplacementVisitor(replacements);
        for (var i = source.Count - 1; i >= 0; i--)
        {
            destination[i].CopyConstraintsFrom(source[i], visitor);
        }
    }
}
