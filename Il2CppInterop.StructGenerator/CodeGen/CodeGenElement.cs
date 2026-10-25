using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal abstract class CodeGenElement
{
    public CodeGenElement(ElementProtection? protection, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        Protection = protection;
        Name = name;
    }

    public abstract string Type { get; }

    public bool IsStatic { get; set; }
    public bool IsUnsafe { get; set; }
    public bool IsPartial { get; set; }
    public string Name { get; }
    public ElementProtection? Protection { get; }

    public string Keywords
    {
        get
        {
            var staticKeyword = IsStatic ? "static " : string.Empty;
            var unsafeKeyword = IsUnsafe ? "unsafe " : string.Empty;
            var partialKeyword = IsPartial ? "partial " : string.Empty;
            return string.Concat(staticKeyword, unsafeKeyword, partialKeyword);
        }
    }

    public virtual string Declaration
    {
        get
        {
            return Protection is null ? $"{Keywords}{Type} {Name}" : $"{Protection.Value.ToCSharpString()} {Keywords}{Type} {Name}";
        }
    }

    public virtual void Build(IndentedTextWriter writer)
    {
        writer.Write(Declaration);
    }
}
