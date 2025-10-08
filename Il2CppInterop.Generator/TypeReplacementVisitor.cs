using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

internal class TypeReplacementVisitor(Dictionary<TypeAnalysisContext, TypeAnalysisContext> replacements) : DefaultTypeVisitor<TypeAnalysisContext>
{
    private readonly Dictionary<TypeAnalysisContext, TypeAnalysisContext> _replacements = replacements;

    public static TypeReplacementVisitor Null { get; } = new NullTypeReplacementVisitor();

    public static TypeReplacementVisitor CreateForMethodCopying(MethodAnalysisContext source, MethodAnalysisContext destination)
    {
        Debug.Assert(source.GenericParameters.Count == destination.GenericParameters.Count);
        Debug.Assert(source.DeclaringType == destination.DeclaringType);
        if (source.GenericParameters.Count == 0)
            return new([]);

        var replacements = new Dictionary<TypeAnalysisContext, TypeAnalysisContext>(source.GenericParameters.Count);
        for (var i = source.GenericParameters.Count - 1; i >= 0; i--)
        {
            replacements.Add(source.GenericParameters[i], destination.GenericParameters[i]);
        }

        return new TypeReplacementVisitor(replacements);
    }

    public static TypeReplacementVisitor Combine(TypeReplacementVisitor first, TypeReplacementVisitor second)
    {
        return new CombinedTypeReplacementVisitor(first, second);
    }

    [return: NotNullIfNotNull(nameof(type))]
    public TypeAnalysisContext? Replace(TypeAnalysisContext? type)
    {
        return type is null ? null : Visit(type);
    }

    public IEnumerable<TypeAnalysisContext> Replace(IEnumerable<TypeAnalysisContext> types)
    {
        foreach (var type in types)
        {
            yield return Replace(type);
        }
    }

    public IReadOnlyList<TypeAnalysisContext> Replace(IReadOnlyList<TypeAnalysisContext> types)
    {
        if (types.Count == 0)
            return [];

        var results = new TypeAnalysisContext[types.Count];
        for (var i = types.Count - 1; i >= 0; i--)
        {
            results[i] = Replace(types[i]);
        }

        return results;
    }

    public void Modify(List<TypeAnalysisContext> types)
    {
        for (var i = 0; i < types.Count; i++)
        {
            types[i] = Replace(types[i]);
        }
    }

    [return: NotNullIfNotNull(nameof(method))]
    public MethodAnalysisContext? Replace(MethodAnalysisContext? method)
    {
        if (method is null)
            return null;

        if (method is not ConcreteGenericMethodAnalysisContext concreteGenericMethod)
            return method;

        var typeArguments = Replace(concreteGenericMethod.TypeGenericParameters);
        var methodArguments = Replace(concreteGenericMethod.MethodGenericParameters);

        return new ConcreteGenericMethodAnalysisContext(concreteGenericMethod.BaseMethodContext, typeArguments, methodArguments);
    }

    [return: NotNullIfNotNull(nameof(field))]
    public FieldAnalysisContext? Replace(FieldAnalysisContext? field)
    {
        if (field is null)
            return null;

        if (field is not ConcreteGenericFieldAnalysisContext concreteGenericField)
            return field;

        var declaringType = (GenericInstanceTypeAnalysisContext)Replace(concreteGenericField.DeclaringType);

        return declaringType == concreteGenericField.DeclaringType ? field : new ConcreteGenericFieldAnalysisContext(concreteGenericField.BaseFieldContext, declaringType);
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

    private sealed class CombinedTypeReplacementVisitor(TypeReplacementVisitor first, TypeReplacementVisitor second) : TypeReplacementVisitor([])
    {
        public override TypeAnalysisContext Visit(TypeAnalysisContext type) => second.Visit(first.Visit(type));
        protected override TypeAnalysisContext Visit(ReferencedTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(WrappedTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(ArrayTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(BoxedTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(ByRefTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(CustomModifierTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(GenericInstanceTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(GenericParameterTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(PinnedTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(PointerTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(SentinelTypeAnalysisContext type) => second.Visit(first.Visit(type));
        public override TypeAnalysisContext Visit(SzArrayTypeAnalysisContext type) => second.Visit(first.Visit(type));
        protected override TypeAnalysisContext VisitSimpleType(TypeAnalysisContext type) => second.Visit(first.Visit(type));
    }

    private sealed class NullTypeReplacementVisitor() : TypeReplacementVisitor([])
    {
        public override TypeAnalysisContext Visit(TypeAnalysisContext type) => type;
        protected override TypeAnalysisContext Visit(ReferencedTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(WrappedTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(ArrayTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(BoxedTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(ByRefTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(CustomModifierTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(GenericInstanceTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(GenericParameterTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(PinnedTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(PointerTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(SentinelTypeAnalysisContext type) => type;
        public override TypeAnalysisContext Visit(SzArrayTypeAnalysisContext type) => type;
        protected override TypeAnalysisContext VisitSimpleType(TypeAnalysisContext type) => type;
    }
}
