using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenEnum : CodeGenElement
{
    public CodeGenEnum(EnumUnderlyingType underlyingType, ElementProtection? protection, string name) : base(protection,
        name)
    {
        UnderlyingType = underlyingType;
    }

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

    public List<CodeGenEnumElement> Elements { get; } = [];

    public override void Build(IndentedTextWriter writer)
    {
        base.Build(writer);
        if (UnderlyingType != EnumUnderlyingType.Int)
            writer.Write($" : {UnderlyingType.ToCSharpString()}");
        writer.WriteLine();
        using (new CurlyBrackets(writer))
        {
            foreach (var element in Elements)
                element.Build(writer);
        }
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

    public override bool Equals(object? obj)
    {
        return obj is CodeGenEnum @enum && this == @enum;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Name, UnderlyingType, Elements.Count);
    }
}
