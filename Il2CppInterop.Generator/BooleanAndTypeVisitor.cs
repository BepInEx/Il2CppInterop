using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public abstract class BooleanAndTypeVisitor : TypeVisitor<bool>
{
    public override bool Visit(ArrayTypeAnalysisContext type) => Visit(type.ElementType);
    public override bool Visit(BoxedTypeAnalysisContext type) => Visit(type.ElementType);
    public override bool Visit(ByRefTypeAnalysisContext type) => Visit(type.ElementType);
    public override bool Visit(CustomModifierTypeAnalysisContext type) => Visit(type.ElementType) && Visit(type.ModifierType);
    public override bool Visit(GenericInstanceTypeAnalysisContext type)
    {
        if (!Visit(type.GenericType))
            return false;

        foreach (var genericArgument in type.GenericArguments)
        {
            if (!Visit(genericArgument))
                return false;
        }

        return true;
    }
    public override bool Visit(GenericParameterTypeAnalysisContext type) => true;
    public override bool Visit(PinnedTypeAnalysisContext type) => Visit(type.ElementType);
    public override bool Visit(PointerTypeAnalysisContext type) => Visit(type.ElementType);
    public override bool Visit(SentinelTypeAnalysisContext type) => true;
    public override bool Visit(SzArrayTypeAnalysisContext type) => Visit(type.ElementType);
    protected override bool VisitSimpleType(TypeAnalysisContext type) => true;
}
