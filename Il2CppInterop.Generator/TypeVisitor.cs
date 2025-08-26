using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public abstract class TypeVisitor<T>
{
    public virtual T Visit(TypeAnalysisContext type) => type switch
    {
        ReferencedTypeAnalysisContext referencedType => Visit(referencedType),
        _ => VisitSimpleType(type),
    };
    protected virtual T Visit(ReferencedTypeAnalysisContext type) => type switch
    {
        WrappedTypeAnalysisContext wrappedType => Visit(wrappedType),
        GenericInstanceTypeAnalysisContext genericInstanceType => Visit(genericInstanceType),
        GenericParameterTypeAnalysisContext genericParameterType => Visit(genericParameterType),
        SentinelTypeAnalysisContext sentinelType => Visit(sentinelType),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Type is not supported"),
    };
    public virtual T Visit(WrappedTypeAnalysisContext type) => type switch
    {
        ArrayTypeAnalysisContext arrayType => Visit(arrayType),
        BoxedTypeAnalysisContext boxedType => Visit(boxedType),
        ByRefTypeAnalysisContext byRefType => Visit(byRefType),
        CustomModifierTypeAnalysisContext customModifierType => Visit(customModifierType),
        PinnedTypeAnalysisContext pinnedType => Visit(pinnedType),
        PointerTypeAnalysisContext pointerType => Visit(pointerType),
        SzArrayTypeAnalysisContext szArrayType => Visit(szArrayType),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Type is not supported"),
    };
    public abstract T Visit(ArrayTypeAnalysisContext type);
    public abstract T Visit(BoxedTypeAnalysisContext type);
    public abstract T Visit(ByRefTypeAnalysisContext type);
    public abstract T Visit(CustomModifierTypeAnalysisContext type);
    public abstract T Visit(GenericInstanceTypeAnalysisContext type);
    public abstract T Visit(GenericParameterTypeAnalysisContext type);
    public abstract T Visit(PinnedTypeAnalysisContext type);
    public abstract T Visit(PointerTypeAnalysisContext type);
    public abstract T Visit(SentinelTypeAnalysisContext type);
    public abstract T Visit(SzArrayTypeAnalysisContext type);
    protected abstract T VisitSimpleType(TypeAnalysisContext type);
}
