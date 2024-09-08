using System.Text;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenEnumElement
{
    public CodeGenEnumElement(string name, string? value = null)
    {
        Name = name;
        Value = value;
    }

    public string Name { get; }
    public string? Value { get; }

    public string BuildFrom(CodeGenEnum origin)
    {
        StringBuilder builder = new($"{origin.IndentInner}{Name}");
        if (Value != null) builder.Append($" = {Value}");
        builder.Append(',');
        return builder.ToString();
    }

    public static bool operator !=(CodeGenEnumElement lhs, CodeGenEnumElement rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenEnumElement lhs, CodeGenEnumElement rhs)
    {
        if (lhs.Name != rhs.Name) return false;
        return lhs.Value == rhs.Value;
    }

    public override bool Equals(object obj)
    {
        return obj is CodeGenEnumElement element && this == element;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, Value);
    }
}

internal enum EnumUnderlyingType
{
    Byte = 0,
    UShort,
    Int,
    UInt,
    ULong
}

internal class CodeGenEnum : CodeGenElement
{
    public CodeGenEnum(EnumUnderlyingType underlyingType, ElementProtection protection, string name) : base(protection,
        name)
    {
        UnderlyingType = underlyingType;
    }

    public override byte IndentAmount { get; set; } = 1;
    public override string Type => "enum";
    public EnumUnderlyingType UnderlyingType { get; set; }

    public int UnderlyingTypeSize => UnderlyingType switch
    {
        EnumUnderlyingType.Byte => 1,
        EnumUnderlyingType.UShort => 2,
        EnumUnderlyingType.Int => 4,
        EnumUnderlyingType.UInt => 4,
        EnumUnderlyingType.ULong => 8,
        _ => throw new Exception("exhausted enum")
    };

    public List<CodeGenEnumElement> Elements { get; } = new();

    public override string Build()
    {
        StringBuilder builder = new(base.Build());
        if (UnderlyingType != EnumUnderlyingType.Int) builder.Append($" : {UnderlyingType.ToString().ToLower()}");
        builder.AppendLine();
        builder.AppendLine($"{Indent}{{");
        foreach (var element in Elements)
            builder.AppendLine(element.BuildFrom(this));
        builder.AppendLine($"{Indent}}}");
        return builder.ToString();
    }

    public static bool operator !=(CodeGenEnum lhs, CodeGenEnum rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenEnum lhs, CodeGenEnum rhs)
    {
        if (lhs.Name != rhs.Name) return false;
        if (lhs.UnderlyingType != rhs.UnderlyingType) return false;
        if (lhs.Elements.Count != rhs.Elements.Count) return false;
        for (var i = 0; i < lhs.Elements.Count; i++)
            if (lhs.Elements[i] != rhs.Elements[i])
                return false;
        return true;
    }

    public override bool Equals(object obj)
    {
        return obj is CodeGenEnum @enum && this == @enum;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, UnderlyingType, Elements.Count);
    }
}
