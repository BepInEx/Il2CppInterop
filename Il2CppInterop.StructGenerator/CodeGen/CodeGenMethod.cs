using System.Text;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenMethod : CodeGenElement
{
    public CodeGenMethod(string returnType, ElementProtection protection, string name) : base(protection, name)
    {
        Type = returnType;
    }

    public override byte IndentAmount { get; set; } = 1;
    public override string Type { get; }

    public List<CodeGenParameter> Parameters { get; } = new();
    public Action<StringBuilder>? MethodBodyBuilder { get; set; } = null;
    public string? ImmediateReturn { get; set; } = null;

    public string BuildBody()
    {
        StringBuilder builder = new();
        if (ImmediateReturn != null)
        {
            if (ImmediateReturn == "") builder.AppendLine(" { }");
            else builder.AppendLine($" => {ImmediateReturn};");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine($"{Indent}{{");
        StringBuilder body = new();
        MethodBodyBuilder?.Invoke(body);
        foreach (var line in body.ToString().Split(Environment.NewLine)) builder.AppendLine($"{IndentInner}{line}");
        builder.AppendLine($"{Indent}}}");
        return builder.ToString();
    }

    public override string Build()
    {
        StringBuilder builder = new($"{base.Build()}({string.Join(", ", Parameters.Select(x => x.Build()))})");
        builder.Append(BuildBody());
        return builder.ToString();
    }
}
