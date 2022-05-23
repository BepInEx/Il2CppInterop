using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenParameter : CodeGenElement
{
    private readonly string myMParameterType;

    public CodeGenParameter(string parameterType, string name) : base(ElementProtection.Private, name)
    {
        myMParameterType = parameterType;
    }

    public override byte IndentAmount { get; set; } = 1;
    public override string Type => myMParameterType;

    public override string Build()
    {
        return $"{myMParameterType} {Name}";
    }
}
