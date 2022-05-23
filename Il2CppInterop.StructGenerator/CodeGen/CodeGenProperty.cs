using System.Text;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenProperty : CodeGenElement
{
    public CodeGenProperty(string propertyType, ElementProtection protection, string name) : base(protection, name)
    {
        Type = propertyType;
    }

    public override byte IndentAmount { get; set; } = 1;
    public override string Type { get; }

    public string? ImmediateGet { get; set; }

    public bool EmptyGet { get; set; }
    public CodeGenMethod? GetMethod { get; set; }

    public bool EmptySet { get; set; }
    public CodeGenMethod? SetMethod { get; set; }

    public override string Build()
    {
        StringBuilder builder = new(base.Build());
        if ((SetMethod == null && GetMethod != null && GetMethod.ImmediateReturn != null) || ImmediateGet != null)
        {
            if (ImmediateGet != null)
                builder.AppendLine($" => {ImmediateGet};");
            else
                builder.Append(GetMethod.BuildBody());
            return builder.ToString();
        }

        if (EmptyGet || EmptySet)
        {
            builder.Append(" {");
            if (EmptyGet) builder.Append(" get;");
            if (EmptySet) builder.Append(" set;");
            builder.AppendLine(" }");
            return builder.ToString();
        }

        builder.AppendLine();
        builder.AppendLine($"{Indent}{{");
        if (GetMethod != null)
        {
            GetMethod.IndentAmount = (byte)(IndentAmount + 1);
            builder.Append($"{IndentInner}get{GetMethod.BuildBody()}");
        }

        if (SetMethod != null)
        {
            SetMethod.IndentAmount = (byte)(IndentAmount + 1);
            builder.Append($"{IndentInner}set{SetMethod.BuildBody()}");
        }

        builder.AppendLine($"{Indent}}}");
        return builder.ToString();
    }
}
