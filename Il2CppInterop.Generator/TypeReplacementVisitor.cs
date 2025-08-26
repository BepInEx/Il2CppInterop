using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal class TypeReplacementVisitor(Dictionary<TypeAnalysisContext, TypeAnalysisContext> replacements) : DefaultTypeVisitor<TypeAnalysisContext>
{
    private readonly Dictionary<TypeAnalysisContext, TypeAnalysisContext> _replacements = replacements;

    [return: NotNullIfNotNull(nameof(type))]
    public TypeAnalysisContext? Replace(TypeAnalysisContext? type)
    {
        return type is null ? null : Visit(type);
    }

    public void Replace(List<TypeAnalysisContext> types)
    {
        for (var i = 0; i < types.Count; i++)
        {
            types[i] = Replace(types[i]);
        }
    }

    protected override TypeAnalysisContext VisitSimpleType(TypeAnalysisContext type)
    {
        return _replacements.TryGetValue(type, out var replacement) ? replacement : type;
    }

    protected override TypeAnalysisContext CombineResults(ArrayTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return type.ElementType == elementResult ? type : elementResult.MakeArrayType(type.Rank);
    }

    protected override TypeAnalysisContext CombineResults(BoxedTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return type.ElementType == elementResult ? type : elementResult.MakeBoxedType();
    }

    protected override TypeAnalysisContext CombineResults(ByRefTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return type.ElementType == elementResult ? type : elementResult.MakeByReferenceType();
    }

    protected override TypeAnalysisContext CombineResults(CustomModifierTypeAnalysisContext type, TypeAnalysisContext elementResult, TypeAnalysisContext modifierResult)
    {
        if (type.ElementType == elementResult && type.ModifierType == modifierResult)
            return type;
        return elementResult.MakeCustomModifierType(modifierResult, type.Required);
    }

    protected override TypeAnalysisContext CombineResults(GenericInstanceTypeAnalysisContext type, TypeAnalysisContext genericTypeResult, TypeAnalysisContext[] genericArgumentsResults)
    {
        if (type.GenericType == genericTypeResult && type.GenericArguments.SequenceEqual(genericArgumentsResults))
            return type;
        return genericTypeResult.MakeGenericInstanceType(genericArgumentsResults!);
    }

    public override TypeAnalysisContext Visit(GenericParameterTypeAnalysisContext type)
    {
        return _replacements.TryGetValue(type, out var replacement) ? replacement : type;
    }

    protected override TypeAnalysisContext CombineResults(PinnedTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return type.ElementType == elementResult ? type : elementResult.MakePinnedType();
    }

    protected override TypeAnalysisContext CombineResults(PointerTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return type.ElementType == elementResult ? type : elementResult.MakePointerType();
    }

    public override TypeAnalysisContext Visit(SentinelTypeAnalysisContext type)
    {
        return _replacements.TryGetValue(type, out var replacement) ? replacement : type;
    }

    protected override TypeAnalysisContext CombineResults(SzArrayTypeAnalysisContext type, TypeAnalysisContext elementResult)
    {
        return type.ElementType == elementResult ? type : elementResult.MakeSzArrayType();
    }
}
