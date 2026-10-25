using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal abstract class CodeGenType : CodeGenElement
{
    public CodeGenType(ElementProtection? protection, string name) : base(protection, name)
    {
    }

    public List<string> InterfaceNames { get; } = [];
    public List<string> Attributes { get; } = [];
    public List<CodeGenMethod> Methods { get; } = [];
    public List<CodeGenField> Fields { get; } = [];
    public List<CodeGenProperty> Properties { get; } = [];
    public List<CodeGenElement> NestedElements { get; } = [];

    public override void Build(IndentedTextWriter writer)
    {
        foreach (var attribute in Attributes)
        {
            writer.WriteLine($"[{attribute}]");
        }
        base.Build(writer);
        if (InterfaceNames.Count > 0)
        {
            writer.Write(" : ");
            writer.Write(InterfaceNames[0]);
            for (var i = 1; i < InterfaceNames.Count; i++)
            {
                writer.Write(", ");
                writer.Write(InterfaceNames[i]);
            }
        }
        writer.WriteLine();
        using (new CurlyBrackets(writer))
        {
            foreach (var method in Methods)
                method.Build(writer);
            foreach (var field in Fields)
                field.Build(writer);
            foreach (var property in Properties)
                property.Build(writer);
            foreach (var nestedElement in NestedElements)
                nestedElement.Build(writer);
        }
    }

    public static bool operator !=(CodeGenType lhs, CodeGenType rhs)
    {
        return !(lhs == rhs);
    }

    public static bool operator ==(CodeGenType lhs, CodeGenType rhs)
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

    public override bool Equals(object? obj)
    {
        return obj is CodeGenType type && this == type;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Fields.Count, NestedElements.Count);
    }
}
