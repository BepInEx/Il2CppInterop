using System.Text;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenFile
{
    private byte myMIndentAmount;
    public string? Namespace { get; set; }
    public List<CodeGenElement> Elements { get; } = new();
    public List<string> Usings { get; } = new();

    private string Indent => new(' ', myMIndentAmount * 4);

    public string Build()
    {
        StringBuilder builder = new();
        foreach (var @using in Usings)
            builder.AppendLine($"using {@using};");
        if (Namespace != null)
        {
            builder.AppendLine($"namespace {Namespace}");
            builder.AppendLine("{");
            myMIndentAmount += 1;
        }

        foreach (var element in Elements)
        {
            element.IndentAmount = (byte)(myMIndentAmount + 1);
            builder.AppendLine($"{Indent}{element.Build()}");
        }

        if (Namespace != null) builder.AppendLine("}");
        return builder.ToString();
    }

    public void WriteTo(string path)
    {
        File.WriteAllText(path, Build());
    }
}
