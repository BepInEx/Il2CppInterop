namespace Il2CppInterop.StructGenerator.CodeGen;

internal sealed class CodeGenInterface : CodeGenType
{
    public CodeGenInterface(ElementProtection? protection, string name) : base(protection, name)
    {
    }

    public override string Type => "interface";
}
