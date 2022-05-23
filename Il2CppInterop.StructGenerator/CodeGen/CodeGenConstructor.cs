using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenConstructor : CodeGenMethod
{
    public CodeGenConstructor(string returnType, ElementProtection protection) : base(returnType, protection,
        "constructor")
    {
    }

    public override string Declaration => $"{Protection.ToString().ToLower()} {Keywords}{Type}";
}
