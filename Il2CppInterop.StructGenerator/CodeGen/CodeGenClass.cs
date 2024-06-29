using System.Text;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenClass : CodeGenElement
{
    public CodeGenClass(ElementProtection protection, string name) : base(protection, name)
    {
    }

    public override byte IndentAmount { get; set; } = 1;
    public override string Type => "class";
    public List<string> InterfaceNames { get; } = new();
    public List<string> Attributes { get; } = new();
    public List<CodeGenMethod> Methods { get; } = new();
    public List<CodeGenField> Fields { get; } = new();
    public List<CodeGenProperty> Properties { get; } = new();
    public List<CodeGenElement> NestedElements { get; } = new();

    public override string Build()
    {
        StringBuilder builder = new();
        if (Attributes.Count > 0)
        {
            for (var i = 0; i < Attributes.Count; i++)
            {
                if (i > 0) builder.Append(Indent);
                builder.AppendLine($"[{Attributes[i]}]");
            }

            builder.Append(Indent);
        }

        builder.Append($"{base.Build()}");
        if (InterfaceNames.Count > 0)
            builder.Append($" : {string.Join(", ", InterfaceNames)}");
        builder.AppendLine();
        builder.AppendLine($"{Indent}{{");
        foreach (var method in Methods)
        {
            method.IndentAmount = (byte)(IndentAmount + 1);
            builder.Append($"{IndentInner}{method.Build()}");
        }

        foreach (var field in Fields)
            builder.AppendLine($"{IndentInner}{field.Build()}");
        foreach (var property in Properties)
        {
            property.IndentAmount = (byte)(IndentAmount + 1);
            builder.Append($"{IndentInner}{property.Build()}");
        }

        foreach (var nestedElement in NestedElements)
        {
            nestedElement.IndentAmount = (byte)(IndentAmount + 1);
            builder.AppendLine($"{IndentInner}{nestedElement.Build()}");
        }

        builder.AppendLine($"{Indent}}}");
        return builder.ToString();
    }

    public static bool operator !=(CodeGenClass lhs, CodeGenClass rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenClass lhs, CodeGenClass rhs)
    {
        if (lhs.Fields.Count != rhs.Fields.Count) return false;
        if (lhs.NestedElements.Count != rhs.NestedElements.Count) return false;
        for (var i = 0; i < lhs.Fields.Count; i++)
            if (lhs.Fields[i] != rhs.Fields[i])
                return false;
        for (var i = 0; i < lhs.NestedElements.Count; i++)
        {
            if (lhs.NestedElements[i] is not CodeGenEnum lhsEnum) continue;
            if (rhs.NestedElements[i] is not CodeGenEnum rhsEnum) continue;
            if (lhsEnum != rhsEnum) return false;
        }

        return true;
    }

    public override bool Equals(object obj)
    {
        return obj is CodeGenClass @class && this == @class;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Fields.Count, NestedElements.Count);
    }
}
