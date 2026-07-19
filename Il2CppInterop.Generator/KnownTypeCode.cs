using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public enum KnownTypeCode
{
    None,
    System_Object,
    System_ValueType,
    System_Enum,
    System_Attribute,
    System_String,
    System_Boolean,
    System_Byte,
    System_SByte,
    System_Int16,
    System_UInt16,
    System_Int32,
    System_UInt32,
    System_Int64,
    System_UInt64,
    System_Single,
    System_Double,
    System_Char,
    System_IntPtr,
    System_UIntPtr,
    System_Void,
    System_Array,
    Il2CppSystem_Object,
    Il2CppSystem_ValueType,
    Il2CppSystem_Enum,
    Il2CppSystem_Attribute,
    Il2CppSystem_String,
    Il2CppSystem_Boolean,
    Il2CppSystem_Byte,
    Il2CppSystem_SByte,
    Il2CppSystem_Int16,
    Il2CppSystem_UInt16,
    Il2CppSystem_Int32,
    Il2CppSystem_UInt32,
    Il2CppSystem_Int64,
    Il2CppSystem_UInt64,
    Il2CppSystem_Single,
    Il2CppSystem_Double,
    Il2CppSystem_Char,
    Il2CppSystem_IntPtr,
    Il2CppSystem_UIntPtr,
    Il2CppSystem_Void,
    Il2CppSystem_IObject,
    Il2CppSystem_IValueType,
    Il2CppSystem_IEnum,
}
internal static class KnownTypeCodeExtensions
{
    extension(KnownTypeCode code)
    {
        public bool IsSystemType => code >= KnownTypeCode.System_Object && code <= KnownTypeCode.System_Array;

        public bool IsIl2CppSystemType => code >= KnownTypeCode.Il2CppSystem_Object && code <= KnownTypeCode.Il2CppSystem_IEnum;

        /// <summary>
        /// Boolean, Char, and all the numeric types
        /// </summary>
        public bool IsIl2CppPrimitiveType => code >= KnownTypeCode.Il2CppSystem_Boolean && code <= KnownTypeCode.Il2CppSystem_UIntPtr;

        public string Name => code switch
        {
            KnownTypeCode.None => "",
            KnownTypeCode.System_Object or KnownTypeCode.Il2CppSystem_Object => "Object",
            KnownTypeCode.System_ValueType or KnownTypeCode.Il2CppSystem_ValueType => "ValueType",
            KnownTypeCode.System_Enum or KnownTypeCode.Il2CppSystem_Enum => "Enum",
            KnownTypeCode.System_Attribute or KnownTypeCode.Il2CppSystem_Attribute => "Attribute",
            KnownTypeCode.System_String or KnownTypeCode.Il2CppSystem_String => "String",
            KnownTypeCode.System_Boolean or KnownTypeCode.Il2CppSystem_Boolean => "Boolean",
            KnownTypeCode.System_Byte or KnownTypeCode.Il2CppSystem_Byte => "Byte",
            KnownTypeCode.System_SByte or KnownTypeCode.Il2CppSystem_SByte => "SByte",
            KnownTypeCode.System_Int16 or KnownTypeCode.Il2CppSystem_Int16 => "Int16",
            KnownTypeCode.System_UInt16 or KnownTypeCode.Il2CppSystem_UInt16 => "UInt16",
            KnownTypeCode.System_Int32 or KnownTypeCode.Il2CppSystem_Int32 => "Int32",
            KnownTypeCode.System_UInt32 or KnownTypeCode.Il2CppSystem_UInt32 => "UInt32",
            KnownTypeCode.System_Int64 or KnownTypeCode.Il2CppSystem_Int64 => "Int64",
            KnownTypeCode.System_UInt64 or KnownTypeCode.Il2CppSystem_UInt64 => "UInt64",
            KnownTypeCode.System_Single or KnownTypeCode.Il2CppSystem_Single => "Single",
            KnownTypeCode.System_Double or KnownTypeCode.Il2CppSystem_Double => "Double",
            KnownTypeCode.System_Char or KnownTypeCode.Il2CppSystem_Char => "Char",
            KnownTypeCode.System_IntPtr or KnownTypeCode.Il2CppSystem_IntPtr => "IntPtr",
            KnownTypeCode.System_UIntPtr or KnownTypeCode.Il2CppSystem_UIntPtr => "UIntPtr",
            KnownTypeCode.System_Void or KnownTypeCode.Il2CppSystem_Void => "Void",
            KnownTypeCode.System_Array => "Array",
            KnownTypeCode.Il2CppSystem_IObject => "IObject",
            KnownTypeCode.Il2CppSystem_IValueType => "IValueType",
            KnownTypeCode.Il2CppSystem_IEnum => "IEnum",
            _ => throw new InvalidOperationException($"Unknown KnownTypeCode: {code}")
        };

        public string Namespace
        {
            get
            {
                if (code.IsSystemType)
                    return "System";
                if (code.IsIl2CppSystemType)
                    return "Il2CppSystem";
                return "";
            }
        }

        public string FullName
        {
            get
            {
                var @namespace = code.Namespace;
                return string.IsNullOrEmpty(@namespace) ? code.Name : $"{@namespace}.{code.Name}";
            }
        }

        public KnownTypeCode ToSystemType() => code switch
        {
            KnownTypeCode.Il2CppSystem_Object => KnownTypeCode.System_Object,
            KnownTypeCode.Il2CppSystem_ValueType => KnownTypeCode.System_ValueType,
            KnownTypeCode.Il2CppSystem_Enum => KnownTypeCode.System_Enum,
            KnownTypeCode.Il2CppSystem_Attribute => KnownTypeCode.System_Attribute,
            KnownTypeCode.Il2CppSystem_String => KnownTypeCode.System_String,
            KnownTypeCode.Il2CppSystem_Boolean => KnownTypeCode.System_Boolean,
            KnownTypeCode.Il2CppSystem_Byte => KnownTypeCode.System_Byte,
            KnownTypeCode.Il2CppSystem_SByte => KnownTypeCode.System_SByte,
            KnownTypeCode.Il2CppSystem_Int16 => KnownTypeCode.System_Int16,
            KnownTypeCode.Il2CppSystem_UInt16 => KnownTypeCode.System_UInt16,
            KnownTypeCode.Il2CppSystem_Int32 => KnownTypeCode.System_Int32,
            KnownTypeCode.Il2CppSystem_UInt32 => KnownTypeCode.System_UInt32,
            KnownTypeCode.Il2CppSystem_Int64 => KnownTypeCode.System_Int64,
            KnownTypeCode.Il2CppSystem_UInt64 => KnownTypeCode.System_UInt64,
            KnownTypeCode.Il2CppSystem_Single => KnownTypeCode.System_Single,
            KnownTypeCode.Il2CppSystem_Double => KnownTypeCode.System_Double,
            KnownTypeCode.Il2CppSystem_Char => KnownTypeCode.System_Char,
            KnownTypeCode.Il2CppSystem_IntPtr => KnownTypeCode.System_IntPtr,
            KnownTypeCode.Il2CppSystem_UIntPtr => KnownTypeCode.System_UIntPtr,
            KnownTypeCode.Il2CppSystem_Void => KnownTypeCode.System_Void,
            KnownTypeCode.Il2CppSystem_IObject => KnownTypeCode.System_Object,
            KnownTypeCode.Il2CppSystem_IValueType => KnownTypeCode.System_ValueType,
            KnownTypeCode.Il2CppSystem_IEnum => KnownTypeCode.System_Enum,
            _ => code
        };

        public AssemblyAnalysisContext GetAssemblyContext(ApplicationAnalysisContext appContext)
        {
            if (code.IsSystemType)
                return appContext.Mscorlib;
            if (code.IsIl2CppSystemType)
                return appContext.Il2CppMscorlib;
            throw new InvalidOperationException($"KnownTypeCode {code} does not correspond to a known assembly");
        }

        public TypeAnalysisContext ToContext(ApplicationAnalysisContext appContext)
        {
            return code.GetAssemblyContext(appContext).GetTypeByFullNameOrThrow(code.FullName);
        }
    }
}
