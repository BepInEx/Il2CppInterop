using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal readonly record struct CodeGenParameter(string Type, string Name)
{
    public void Build(IndentedTextWriter writer)
    {
        writer.Write(Type);
        writer.Write(' ');
        writer.Write(Name);
    }
}
