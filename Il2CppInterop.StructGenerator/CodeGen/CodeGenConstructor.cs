namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenConstructor : CodeGenMethod
{
    public CodeGenConstructor(string returnType, ElementProtection? protection) : base(returnType, protection,
        "constructor")
    {
    }

    public override string Declaration
    {
        get
        {
            return Protection is null ? $"{Keywords}{Type}" : $"{Protection.Value.ToCSharpString()} {Keywords}{Type}";
        }
    }
}
