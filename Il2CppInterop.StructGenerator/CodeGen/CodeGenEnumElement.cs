using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal readonly record struct CodeGenEnumElement(string Name, string? Value = null)
{
    public void Build(IndentedTextWriter writer)
    {
        writer.Write(Name);
        if (Value != null)
            writer.Write($" = {Value}");
        writer.WriteLine(',');
    }
}
