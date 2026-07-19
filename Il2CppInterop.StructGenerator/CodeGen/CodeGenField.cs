using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenField : CodeGenElement
{
    public CodeGenField(string type, ElementProtection? protection, string name) : base(protection, name)
    {
        ArgumentException.ThrowIfNullOrEmpty(type);
        FieldType = type;
    }

    public override string Type => FieldType;

    public string? DefaultValue { get; set; } = null;
    public string FieldType { get; set; }

    public override void Build(IndentedTextWriter writer)
    {
        base.Build(writer);
        if (DefaultValue != null)
            writer.Write($" = {DefaultValue}");
        writer.WriteLine(';');
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

    public override bool Equals(object? obj)
    {
        return obj is CodeGenField field && this == field;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Type, Name, DefaultValue);
    }
}
