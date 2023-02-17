using System;
using System.Runtime.InteropServices;

// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable IdentifierTypo
// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Global

namespace Il2CppInterop.Runtime.Runtime;

//Stub structs
public struct Il2CppAssembly
{
}

public struct Il2CppClass
{
}

public struct Il2CppEventInfo
{
}

public struct Il2CppException
{
}

public struct Il2CppFieldInfo
{
}

public struct Il2CppImage
{
}

public struct Il2CppMethodInfo
{
}

public struct Il2CppParameterInfo
{
}

public struct Il2CppPropertyInfo
{
}

public struct Il2CppTypeStruct
{
}

public struct Il2CppAssemblyName
{
}

public struct Il2CppString
{
}

public struct Il2CppMetadataTypeHandle
{
    private readonly unsafe void* dummy;
}

public struct Il2CppMetadataGenericContainerHandle
{
    private readonly unsafe void* dummy;
}

public struct Il2CppMetadataImageHandle
{
    private readonly unsafe void* dummy;
}

public struct il2cpp_hresult_t
{
    private readonly int dummy;
}

public struct Il2CppGCHandle
{
    private readonly unsafe void* dummy;
}

[Flags]
public enum Il2CppMethodImplFlags : ushort
{
    METHOD_IMPL_ATTRIBUTE_CODE_TYPE_MASK = 0x0003,
    METHOD_IMPL_ATTRIBUTE_IL = 0x0000,
    METHOD_IMPL_ATTRIBUTE_NATIVE = 0x0001,
    METHOD_IMPL_ATTRIBUTE_OPTIL = 0x0002,
    METHOD_IMPL_ATTRIBUTE_RUNTIME = 0x0003,

    METHOD_IMPL_ATTRIBUTE_MANAGED_MASK = 0x0004,
    METHOD_IMPL_ATTRIBUTE_UNMANAGED = 0x0004,
    METHOD_IMPL_ATTRIBUTE_MANAGED = 0x0000,

    METHOD_IMPL_ATTRIBUTE_FORWARD_REF = 0x0010,
    METHOD_IMPL_ATTRIBUTE_PRESERVE_SIG = 0x0080,
    METHOD_IMPL_ATTRIBUTE_INTERNAL_CALL = 0x1000,
    METHOD_IMPL_ATTRIBUTE_SYNCHRONIZED = 0x0020,
    METHOD_IMPL_ATTRIBUTE_NOINLINING = 0x0008,
    METHOD_IMPL_ATTRIBUTE_MAX_METHOD_IMPL_VAL = 0xffff
}

[Flags]
public enum Il2CppMethodFlags : ushort
{
    METHOD_ATTRIBUTE_MEMBER_ACCESS_MASK = 0x0007,
    METHOD_ATTRIBUTE_COMPILER_CONTROLLED = 0x0000,
    METHOD_ATTRIBUTE_PRIVATE = 0x0001,
    METHOD_ATTRIBUTE_FAM_AND_ASSEM = 0x0002,
    METHOD_ATTRIBUTE_ASSEM = 0x0003,
    METHOD_ATTRIBUTE_FAMILY = 0x0004,
    METHOD_ATTRIBUTE_FAM_OR_ASSEM = 0x0005,
    METHOD_ATTRIBUTE_PUBLIC = 0x0006,

    METHOD_ATTRIBUTE_STATIC = 0x0010,
    METHOD_ATTRIBUTE_FINAL = 0x0020,
    METHOD_ATTRIBUTE_VIRTUAL = 0x0040,
    METHOD_ATTRIBUTE_HIDE_BY_SIG = 0x0080,

    METHOD_ATTRIBUTE_VTABLE_LAYOUT_MASK = 0x0100,
    METHOD_ATTRIBUTE_REUSE_SLOT = 0x0000,
    METHOD_ATTRIBUTE_NEW_SLOT = 0x0100,

    METHOD_ATTRIBUTE_STRICT = 0x0200,
    METHOD_ATTRIBUTE_ABSTRACT = 0x0400,
    METHOD_ATTRIBUTE_SPECIAL_NAME = 0x0800,

    METHOD_ATTRIBUTE_PINVOKE_IMPL = 0x2000,
    METHOD_ATTRIBUTE_UNMANAGED_EXPORT = 0x0008,

    /*
     * For runtime use only
     */
    METHOD_ATTRIBUTE_RESERVED_MASK = 0xd000,
    METHOD_ATTRIBUTE_RT_SPECIAL_NAME = 0x1000,
    METHOD_ATTRIBUTE_HAS_SECURITY = 0x4000,
    METHOD_ATTRIBUTE_REQUIRE_SEC_OBJECT = 0x8000
}

[Flags]
public enum Il2CppClassAttributes : uint
{
    TYPE_ATTRIBUTE_VISIBILITY_MASK = 0x00000007,
    TYPE_ATTRIBUTE_NOT_PUBLIC = 0x00000000,
    TYPE_ATTRIBUTE_PUBLIC = 0x00000001,
    TYPE_ATTRIBUTE_NESTED_PUBLIC = 0x00000002,
    TYPE_ATTRIBUTE_NESTED_PRIVATE = 0x00000003,
    TYPE_ATTRIBUTE_NESTED_FAMILY = 0x00000004,
    TYPE_ATTRIBUTE_NESTED_ASSEMBLY = 0x00000005,
    TYPE_ATTRIBUTE_NESTED_FAM_AND_ASSEM = 0x00000006,
    TYPE_ATTRIBUTE_NESTED_FAM_OR_ASSEM = 0x00000007,

    TYPE_ATTRIBUTE_LAYOUT_MASK = 0x00000018,
    TYPE_ATTRIBUTE_AUTO_LAYOUT = 0x00000000,
    TYPE_ATTRIBUTE_SEQUENTIAL_LAYOUT = 0x00000008,
    TYPE_ATTRIBUTE_EXPLICIT_LAYOUT = 0x00000010,

    TYPE_ATTRIBUTE_CLASS_SEMANTIC_MASK = 0x00000020,
    TYPE_ATTRIBUTE_CLASS = 0x00000000,
    TYPE_ATTRIBUTE_INTERFACE = 0x00000020,

    TYPE_ATTRIBUTE_ABSTRACT = 0x00000080,
    TYPE_ATTRIBUTE_SEALED = 0x00000100,
    TYPE_ATTRIBUTE_SPECIAL_NAME = 0x00000400,

    TYPE_ATTRIBUTE_IMPORT = 0x00001000,
    TYPE_ATTRIBUTE_SERIALIZABLE = 0x00002000,

    TYPE_ATTRIBUTE_STRING_FORMAT_MASK = 0x00030000,
    TYPE_ATTRIBUTE_ANSI_CLASS = 0x00000000,
    TYPE_ATTRIBUTE_UNICODE_CLASS = 0x00010000,
    TYPE_ATTRIBUTE_AUTO_CLASS = 0x00020000,

    TYPE_ATTRIBUTE_BEFORE_FIELD_INIT = 0x00100000,
    TYPE_ATTRIBUTE_FORWARDER = 0x00200000,

    TYPE_ATTRIBUTE_RESERVED_MASK = 0x00040800,
    TYPE_ATTRIBUTE_RT_SPECIAL_NAME = 0x00000800,
    TYPE_ATTRIBUTE_HAS_SECURITY = 0x00040000
}

public enum Il2CppTypeEnum : byte
{
    IL2CPP_TYPE_END = 0x00, /* End of List */
    IL2CPP_TYPE_VOID = 0x01,
    IL2CPP_TYPE_BOOLEAN = 0x02,
    IL2CPP_TYPE_CHAR = 0x03,
    IL2CPP_TYPE_I1 = 0x04,
    IL2CPP_TYPE_U1 = 0x05,
    IL2CPP_TYPE_I2 = 0x06,
    IL2CPP_TYPE_U2 = 0x07,
    IL2CPP_TYPE_I4 = 0x08,
    IL2CPP_TYPE_U4 = 0x09,
    IL2CPP_TYPE_I8 = 0x0a,
    IL2CPP_TYPE_U8 = 0x0b,
    IL2CPP_TYPE_R4 = 0x0c,
    IL2CPP_TYPE_R8 = 0x0d,
    IL2CPP_TYPE_STRING = 0x0e,
    IL2CPP_TYPE_PTR = 0x0f, /* arg: <type> token */
    IL2CPP_TYPE_BYREF = 0x10, /* arg: <type> token */
    IL2CPP_TYPE_VALUETYPE = 0x11, /* arg: <type> token */
    IL2CPP_TYPE_CLASS = 0x12, /* arg: <type> token */

    IL2CPP_TYPE_VAR =
        0x13, /* Generic parameter in a generic type definition, represented as number (compressed unsigned integer) number */
    IL2CPP_TYPE_ARRAY = 0x14, /* type, rank, boundsCount, bound1, loCount, lo1 */
    IL2CPP_TYPE_GENERICINST = 0x15, /* <type> <type-arg-count> <type-1> \x{2026} <type-n> */
    IL2CPP_TYPE_TYPEDBYREF = 0x16,
    IL2CPP_TYPE_I = 0x18,
    IL2CPP_TYPE_U = 0x19,
    IL2CPP_TYPE_FNPTR = 0x1b, /* arg: full method signature */
    IL2CPP_TYPE_OBJECT = 0x1c,
    IL2CPP_TYPE_SZARRAY = 0x1d, /* 0-based one-dim-array */

    IL2CPP_TYPE_MVAR =
        0x1e, /* Generic parameter in a generic method definition, represented as number (compressed unsigned integer)  */
    IL2CPP_TYPE_CMOD_REQD = 0x1f, /* arg: typedef or typeref token */
    IL2CPP_TYPE_CMOD_OPT = 0x20, /* optional arg: typedef or typref token */
    IL2CPP_TYPE_INTERNAL = 0x21, /* CLR internal type */

    IL2CPP_TYPE_MODIFIER = 0x40, /* Or with the following types */
    IL2CPP_TYPE_SENTINEL = 0x41, /* Sentinel for varargs method signature */
    IL2CPP_TYPE_PINNED = 0x45, /* Local var that points to pinned object */

    IL2CPP_TYPE_ENUM = 0x55 /* an enumeration */
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct VirtualInvokeData
{
    public IntPtr methodPtr;
    public Il2CppMethodInfo* method;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Il2CppRuntimeInterfaceOffsetPair
{
    public Il2CppClass* interfaceType;
    public int offset;
}

[StructLayout(LayoutKind.Sequential)]
public struct Il2CppObject
{
    private readonly IntPtr data;
    private readonly IntPtr monitor;
}

public struct Il2CppImageGlobalMetadata
{
    public int typeStart;
    public int exportedTypeStart;
    public int customAttributeStart;
    public int entryPointIndex;
    public unsafe Il2CppImage* image;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Il2CppGenericInst
{
    public uint type_argc;
    public Il2CppTypeStruct** type_argv;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Il2CppGenericContext
{
    /* The instantiation corresponding to the class generic parameters */
    public Il2CppGenericInst* class_inst;

    /* The instantiation corresponding to the method generic parameters */
    public Il2CppGenericInst* method_inst;
}

[StructLayout(LayoutKind.Sequential)]
public unsafe struct Il2CppGenericMethod
{
    public Il2CppMethodInfo* methodDefinition;
    public Il2CppGenericContext context;
}

public unsafe struct Il2CppReflectionMethod
{
    public Il2CppObject _object;
    public Il2CppMethodInfo* method;
    public Il2CppString* name;
    public IntPtr reftype; // Il2CppReflectionType*
}
