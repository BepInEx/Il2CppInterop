using System.Text;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenField : CodeGenElement
{
    public CodeGenField(string type, ElementProtection protection, string name) : base(protection, name)
    {
        FieldType = type;
    }

    public override byte IndentAmount { get; set; } = 1;
    public override string Type => FieldType;

    public string? DefaultValue { get; set; } = null;
    public string FieldType { get; set; }

    public override string Build()
    {
        StringBuilder builder = new($"{base.Build()}");
        if (DefaultValue != null) builder.Append($" = {DefaultValue}");
        builder.Append(';');
        return builder.ToString();
    }

    public static bool operator !=(CodeGenField lhs, CodeGenField rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenField lhs, CodeGenField rhs)
    {
        if (lhs.Type != rhs.Type) return false;
        if (lhs.Name != rhs.Name) return false;
        return lhs.DefaultValue == rhs.DefaultValue;
    }

    public override bool Equals(object obj)
    {
        return obj is CodeGenField field && this == field;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name, DefaultValue);
    }
}
