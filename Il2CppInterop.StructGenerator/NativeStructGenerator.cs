using System.Diagnostics;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator;

internal class NativeStructGenerator
{
    public NativeStructGenerator(string metadataSuffix, CppClass cppClass)
    {
        CppClass = cppClass;
        NativeStruct = new CodeGenStruct(ElementProtection.Internal, $"{cppClass.Name}_{metadataSuffix}")
        {
            IsUnsafe = true
        };
        FillStruct();
    }

    /// <summary>
    /// Fields whose types might need to be imported from other namespaces
    /// </summary>
    public List<(CodeGenField, CppField)> FieldsToImport { get; } = [];
    public CodeGenStruct NativeStruct { get; }
    public CppClass CppClass { get; }

    private void FillStruct()
    {
        List<CodeGenEnum> bitfields = [];
        CodeGenEnum? lastBitfield = null;
        var bitfieldNextBit = 0;

        bool VisitBitfieldElement(CppField field)
        {
            if (field.BitFieldWidth == 16)
            {
                NativeStruct.Fields.Add(new CodeGenField("ushort", ElementProtection.Public, field.Name));
                return true;
            }

            if (field.BitFieldWidth == 8)
            {
                NativeStruct.Fields.Add(new CodeGenField("byte", ElementProtection.Public, field.Name));
                return true;
            }

            if (lastBitfield is null)
                lastBitfield = new CodeGenEnum(EnumUnderlyingType.Byte, ElementProtection.Internal,
                    $"Bitfield{bitfields.Count}");
            if (field.BitFieldWidth != 1)
            {
                bitfieldNextBit += field.BitFieldWidth;
                return false;
            }

            var bitIdx = bitfieldNextBit++;
            lastBitfield.Elements.Add(new CodeGenEnumElement($"BIT_{field.Name}", $"{bitIdx}"));
            lastBitfield.Elements.Add(
                new CodeGenEnumElement(field.Name, $"({field.BitFieldWidth} << BIT_{field.Name})"));
            if (lastBitfield.UnderlyingTypeSize * 8 < bitfieldNextBit)
                lastBitfield.UnderlyingType += 1;
            return false;
        }

        void FinalizeBitfield()
        {
            if (lastBitfield is null) return;
            NativeStruct.Fields.Add(new CodeGenField(lastBitfield.Name, ElementProtection.Public,
                $"_{lastBitfield.Name.ToLower()}"));
            bitfields.Add(lastBitfield);
            lastBitfield = null;
            bitfieldNextBit = 0;
        }

        Debug.Assert(CppClass.BaseTypes.Count == 0);

        foreach (var field in CppClass.Fields)
        {
            var normalizedType = ConversionUtils.CppTypeToCSharpName(field.Type, out var needsImport);
            if (string.IsNullOrEmpty(normalizedType)) continue;

            if (field.IsBitField)
            {
                if (bitfieldNextBit == 8) FinalizeBitfield();
                if (VisitBitfieldElement(field)) FinalizeBitfield();
            }
            else
            {
                FinalizeBitfield();
                CodeGenField codeGenField = new(normalizedType, ElementProtection.Public, GetFieldName(field));
                if (needsImport)
                    FieldsToImport.Add((codeGenField, field));
                NativeStruct.Fields.Add(codeGenField);
            }
        }

        FinalizeBitfield();
        NativeStruct.NestedElements.AddRange(bitfields);
    }

    private static string GetFieldName(CppField field)
    {
        var name = field.Name;
        if (name is "object" or "class" or "struct" or "base")
        {
            return $"_{name}";
        }
        else if (name.Length == 0)
        {
            if (field.Parent is CppClass { Name: "Il2CppMethodInfo" } && field.Type is CppClass { ClassKind: CppClassKind.Union } unionType)
            {
                // IlCppMethodInfo has two unnamed union fields
                if (unionType.Fields.Any(f => f.Name is "rgctx_data"))
                {
                    return "runtime_data";
                }
                if (unionType.Fields.Any(f => f.Name is "genericMethod"))
                {
                    return "generic_data";
                }
            }
            throw new ArgumentException("Field has no name and is not part of a known union", nameof(field));
        }
        else
        {
            return name;
        }
    }
}
