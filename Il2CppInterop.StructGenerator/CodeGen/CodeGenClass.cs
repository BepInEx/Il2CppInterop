namespace Il2CppInterop.StructGenerator.CodeGen;

internal sealed class CodeGenClass : CodeGenType
{
    public CodeGenClass(ElementProtection? protection, string name) : base(protection, name)
    {
    }

    public override string Type => "class";
}
