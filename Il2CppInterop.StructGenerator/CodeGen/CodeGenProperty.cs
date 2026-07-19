using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator.CodeGen;

internal class CodeGenProperty : CodeGenElement
{
    public CodeGenProperty(string propertyType, ElementProtection? protection, string name) : base(protection, name)
    {
        Type = propertyType;
    }

    public override string Type { get; }

    public string? ImmediateGet { get; set; }

    public bool EmptyGet { get; set; }
    public CodeGenMethod? GetMethod { get; set; }

    public bool EmptySet { get; set; }
    public CodeGenMethod? SetMethod { get; set; }

    public bool HasGet => GetMethod != null || ImmediateGet != null || EmptyGet;
    public bool HasSet => SetMethod != null || EmptySet;

    /// <summary>
    /// An optional initializer for the property. If provided, this will be used to initialize the property with a default value.
    /// </summary>
    /// <remarks>
    /// This is only used if either <see cref="EmptyGet"/> or <see cref="EmptySet"/> is <see langword="true"/>.
    /// </remarks>
    public string? Initializer { get; set; }

    public override void Build(IndentedTextWriter writer)
    {
        base.Build(writer);
        if (ImmediateGet != null)
        {
            writer.WriteLine($" => {ImmediateGet};");
        }
        else if (SetMethod == null && GetMethod != null && GetMethod.ImmediateReturn != null)
        {
            GetMethod.BuildBody(writer);
        }
        else if (EmptyGet || EmptySet)
        {
            writer.Write(" {");
            if (EmptyGet) writer.Write(" get;");
            if (EmptySet) writer.Write(" set;");
            if (!string.IsNullOrEmpty(Initializer))
            {
                writer.WriteLine($" }} = {Initializer};");
            }
            else
            {

                writer.WriteLine(" }");
            }
        }
        else
        {
            writer.WriteLine();
            using (new CurlyBrackets(writer))
            {
                if (GetMethod != null)
                {
                    writer.Write("get");
                    GetMethod.BuildBody(writer);
                }
                if (SetMethod != null)
                {
                    writer.Write("set");
                    SetMethod.BuildBody(writer);
                }
            }
        }
    }
}
