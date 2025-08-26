using System.Diagnostics.CodeAnalysis;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator.Operands;

public sealed class LocalVariable
{
    public required TypeAnalysisContext Type { get; init; }

    public LocalVariable()
    {
    }

    [SetsRequiredMembers]
    public LocalVariable(TypeAnalysisContext type)
    {
        Type = type;
    }
}
