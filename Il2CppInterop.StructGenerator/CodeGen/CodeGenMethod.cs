using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenMethod : CodeGenElement
{
    public CodeGenMethod(string returnType, ElementProtection? protection, string name) : base(protection, name)
    {
        Type = returnType;
    }

    public override string Type { get; }

    public List<CodeGenParameter> Parameters { get; } = [];
    public Action<IndentedTextWriter>? MethodBodyBuilder { get; set; } = null;
    public string? ImmediateReturn { get; set; } = null;
    public bool HasBody { get; set; } = true;

    public void BuildBody(IndentedTextWriter writer)
    {
        if (!HasBody)
        {
            writer.WriteLine(";");
        }
        else if (ImmediateReturn != null)
        {
            if (ImmediateReturn == "")
                writer.WriteLine(" { }");
            else
                writer.WriteLine($" => {ImmediateReturn};");
        }
        else
        {
            writer.WriteLine();
            using (new CurlyBrackets(writer))
            {
                MethodBodyBuilder?.Invoke(writer);
            }
        }
    }

    public override void Build(IndentedTextWriter writer)
    {
        base.Build(writer);
        writer.Write('(');
        for (var i = 0; i < Parameters.Count; i++)
        {
            Parameters[i].Build(writer);
            if (i != Parameters.Count - 1)
                writer.Write(", ");
        }
        writer.Write(')');
        BuildBody(writer);
    }
}
