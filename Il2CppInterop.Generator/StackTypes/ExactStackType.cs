using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator.StackTypes;

public sealed record class ExactStackType(TypeAnalysisContext Type) : StackType
{
    public bool Equals(ExactStackType? other)
    {
        return TypeAnalysisContextEqualityComparer.Instance.Equals(Type, other?.Type);
    }

    public override int GetHashCode()
    {
        return TypeAnalysisContextEqualityComparer.Instance.GetHashCode(Type);
    }

    public override string ToString() => Type.FullName;
}
