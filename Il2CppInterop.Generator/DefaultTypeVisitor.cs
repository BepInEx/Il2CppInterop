using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public abstract class DefaultTypeVisitor<T> : TypeVisitor<T>
{
    public override T Visit(ArrayTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType));
    public override T Visit(BoxedTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType));
    public override T Visit(ByRefTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType));
    public override T Visit(CustomModifierTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType), Visit(type.ModifierType));
    public override T Visit(GenericInstanceTypeAnalysisContext type)
    {
        var genericTypeResult = Visit(type.GenericType);
        var genericArgumentsResults = new T[type.GenericArguments.Count];
        for (var i = 0; i < type.GenericArguments.Count; i++)
        {
            genericArgumentsResults[i] = Visit(type.GenericArguments[i]);
        }
        return CombineResults(type, genericTypeResult, genericArgumentsResults);
    }
    /// <summary>
    /// If not overridden, this will return <see langword="default"/>.
    /// </summary>
    public override T Visit(GenericParameterTypeAnalysisContext type) => default!;
    public override T Visit(PinnedTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType));
    public override T Visit(PointerTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType));
    /// <summary>
    /// If not overridden, this will return <see langword="default"/>.
    /// </summary>
    public override T Visit(SentinelTypeAnalysisContext type) => default!;
    public override T Visit(SzArrayTypeAnalysisContext type) => CombineResults(type, Visit(type.ElementType));
    /// <summary>
    /// If not overridden, this will return <see langword="default"/>.
    /// </summary>
    protected override T VisitSimpleType(TypeAnalysisContext type) => default!;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(ArrayTypeAnalysisContext type, T elementResult) => elementResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(BoxedTypeAnalysisContext type, T elementResult) => elementResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(ByRefTypeAnalysisContext type, T elementResult) => elementResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(CustomModifierTypeAnalysisContext type, T elementResult, T modifierResult) => elementResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="genericTypeResult"/>.
    /// </summary>
    protected virtual T CombineResults(GenericInstanceTypeAnalysisContext type, T genericTypeResult, T[] genericArgumentsResults) => genericTypeResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(PinnedTypeAnalysisContext type, T elementResult) => elementResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(PointerTypeAnalysisContext type, T elementResult) => elementResult;
    /// <summary>
    /// If not overridden, this will return the <paramref name="elementResult"/>.
    /// </summary>
    protected virtual T CombineResults(SzArrayTypeAnalysisContext type, T elementResult) => elementResult;
}
