namespace Il2CppInterop.StructGenerator.CodeGen;

internal sealed class CodeGenStruct : CodeGenType
{
    public CodeGenStruct(ElementProtection? protection, string name) : base(protection, name)
    {
    }

    public override string Type => "struct";
}
