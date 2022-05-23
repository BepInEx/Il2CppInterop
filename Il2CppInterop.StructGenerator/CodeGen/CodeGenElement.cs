using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal abstract class CodeGenElement
{
    public CodeGenElement(ElementProtection protection, string name)
    {
        Protection = protection;
        Name = name;
    }

    public abstract byte IndentAmount { get; set; }
    public abstract string Type { get; }

    public bool IsStatic { get; set; }
    public bool IsUnsafe { get; set; }
    public string Name { get; }
    public ElementProtection Protection { get; }
    public string Indent => new(' ', (IndentAmount - 1) * 4);
    public string IndentInner => new(' ', IndentAmount * 4);

    private List<string> KeywordList
    {
        get
        {
            var list = new List<string>();
            if (IsStatic) list.Add("static");
            if (IsUnsafe) list.Add("unsafe");
            return list;
        }
    }

    public string Keywords => KeywordList.Count > 0 ? $"{string.Join(' ', KeywordList)} " : string.Empty;
    public virtual string Declaration => $"{Protection.ToString().ToLower()} {Keywords}{Type} {Name}";

    public virtual string Build()
    {
        return $"{Declaration}";
    }
}
