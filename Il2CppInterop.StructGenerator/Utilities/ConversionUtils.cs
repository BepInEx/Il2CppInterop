using CppAst;
using Il2CppInterop.StructGenerator.Resources;

namespace Il2CppInterop.StructGenerator.Utilities;

internal static class ConversionUtils
{
    private static readonly Dictionary<string, string> STypeRenames = new()
    {
        ["Il2CppType"] = "Il2CppTypeStruct",
        ["FieldInfo"] = "Il2CppFieldInfo",
        ["MethodInfo"] = "Il2CppMethodInfo",
        ["PropertyInfo"] = "Il2CppPropertyInfo",
        ["ParameterInfo"] = "Il2CppParameterInfo",
        ["EventInfo"] = "Il2CppEventInfo"
    };

    private static readonly Dictionary<string, string> STypeConversions = new()
    {
        // Not stubbed
        ["Il2CppArray"] = "void",
        ["Il2CppGenericClass"] = "void",
        ["Il2CppTypeDefinition"] = "void",
        ["Il2CppInteropData"] = "void",
        ["Il2CppRGCTXData"] = "void",
        ["Il2CppCodeGenModule"] = "void",
        ["Il2CppNameToTypeDefinitionIndexHashTable"] = "void",
        ["Il2CppNameToTypeHandleHashTable"] = "void",

        ["methodPointerType"] = "void*",
        ["Il2CppMethodPointer"] = "void*",
        ["InvokerMethod"] = "void*",

        ["TypeIndex"] = "int",
        ["TypeDefinitionIndex"] = "int",
        ["FieldIndex"] = "int",
        ["DefaultValueIndex"] = "int",
        ["DefaultValueDataIndex"] = "int",
        ["CustomAttributeIndex"] = "int",
        ["ParameterIndex"] = "int",
        ["MethodIndex"] = "int",
        ["GenericMethodIndex"] = "int",
        ["PropertyIndex"] = "int",
        ["EventIndex"] = "int",
        ["GenericContainerIndex"] = "int",
        ["GenericParameterIndex"] = "int",
        ["GenericParameterConstraintIndex"] = "short",
        ["NestedTypeIndex"] = "int",
        ["InterfacesIndex"] = "int",
        ["VTableIndex"] = "int",
        ["InterfaceOffsetIndex"] = "int",
        ["RGCTXIndex"] = "int",
        ["StringIndex"] = "int",
        ["StringLiteralIndex"] = "int",
        ["GenericInstIndex"] = "int",
        ["ImageIndex"] = "int",
        ["AssemblyIndex"] = "int",
        ["InteropDataIndex"] = "int",

        ["char"] = "byte",
        ["uint8_t"] = "byte",
        ["uint16_t"] = "ushort",
        ["int32_t"] = "int",
        ["uint32_t"] = "uint",
        ["unsigned int"] = "uint",
        ["uint64_t"] = "ulong",
        ["size_t"] = "IntPtr"
    };

    private static readonly string[] SInvalidNames =
    {
        "object",
        "class",
        "struct",
        "base"
    };

    public static string NormalizeName(string name)
    {
        return SInvalidNames.Contains(name) ? $"_{name}" : name;
    }

    public static string CppTypeToCSharpName(CppType type, out bool needsImport)
    {
        needsImport = false;

        if (type is CppArrayType arrayType)
        {
            if (arrayType.SizeOf == 0) return "";
            if (arrayType.SizeOf == 4) return "uint";
            if (arrayType.SizeOf == 8) return "ulong";
            return $"{CppTypeToCSharpName(arrayType.ElementType, out needsImport)}*";
        }

        if (type is CppClass fieldType && fieldType.ClassKind == CppClassKind.Union) return "void*";
        // Forgive me for my sins
        var oldTypeName = type.GetDisplayName().Replace("const ", string.Empty);
        var ptrCount = oldTypeName.Count(x => x == '*');
        if (ptrCount == 0 && Config.ClassToGenerator.ContainsKey(oldTypeName))
            needsImport = true;

        string ptrs = new('*', ptrCount);
        oldTypeName = oldTypeName.Replace("*", string.Empty);
        if (STypeRenames.ContainsKey(oldTypeName))
            oldTypeName = STypeRenames[oldTypeName];
        return STypeConversions.TryGetValue(oldTypeName, out var converted)
            ? $"{converted!}{ptrs}"
            : $"{oldTypeName}{ptrs}";
    }
}
