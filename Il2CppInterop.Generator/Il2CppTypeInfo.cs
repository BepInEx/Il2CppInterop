using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public sealed class Il2CppTypeInfo
{
    public List<FieldAnalysisContext> StaticFields { get; } = new();
    public List<FieldAnalysisContext> InstanceFields { get; } = new();
    public TypeBlittability Blittability { get; set; } = TypeBlittability.Unknown;
}
