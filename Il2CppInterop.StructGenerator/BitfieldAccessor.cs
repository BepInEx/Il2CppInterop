using System.CodeDom.Compiler;

namespace Il2CppInterop.StructGenerator;

internal class BitfieldAccessor
{
    public BitfieldAccessor(string accessorName, string elementName, string accessorType = "bool",
        bool generateIfNotPresent = true, string? defaultGetter = "", Action<IndentedTextWriter>? defaultGetBuilder = null,
        Action<IndentedTextWriter>? defaultSetBuilder = null)
    {
        AccessorName = accessorName;
        ElementName = elementName;
        AccessorType = accessorType;
        GenerateIfNotPresent = generateIfNotPresent;
        DefaultImmediateGetter = defaultGetter;
        DefaultGetBuilder = defaultGetBuilder;
        DefaultSetBuilder = defaultSetBuilder;
    }

    public string AccessorName { get; }
    public string ElementName { get; }
    public string AccessorType { get; }
    public bool GenerateIfNotPresent { get; }
    public string? DefaultImmediateGetter { get; }
    public Action<IndentedTextWriter>? DefaultGetBuilder { get; }
    public Action<IndentedTextWriter>? DefaultSetBuilder { get; }
}
