using System.CodeDom.Compiler;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppClassGenerator : VersionSpecificGenerator
{
    public Il2CppClassGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
        var lastField = NativeStructGenerator.NativeStruct.Fields[^1];
        if (lastField.Name == "vtable")
            NativeStructGenerator.NativeStruct.Fields.RemoveAt(NativeStructGenerator.NativeStruct.Fields.Count - 1);
        ExtraUsings.Add("Il2CppInterop.Runtime.Structs.VersionSpecific.Type");
    }

    public override string GeneratorName => "Class";

    protected override IEnumerable<CodeGenParameter>? CreateNewParameters => [new CodeGenParameter("int", "vTableSlots")];

    protected override string? SizeOverride => "Size + sizeof(VirtualInvokeData) * vTableSlots";
    private bool ByValArgIsPointer => GetNativeField("byval_arg")?.FieldType.EndsWith('*') ?? false;
    private bool ThisArgIsPointer => GetNativeField("this_arg")?.FieldType.EndsWith('*') ?? false;

    protected override Action<IndentedTextWriter>? CreateNewExtraBody => writer =>
    {
        if (GetNativeField("vtable") is not null)
        {
            writer.WriteLine("Marshal.FreeHGlobal(ptr);");
            writer.WriteLine(
                $"throw new System.NotSupportedException(\"The native struct '{NativeStructGenerator.NativeStruct.Name}' has a vtable field which is not currently supported!\");");
            return;
        }

        if (ByValArgIsPointer)
            writer.WriteLine("_->byval_arg = UnityVersionHandler.NewType().TypePointer;");
        if (ThisArgIsPointer)
            writer.WriteLine("_->this_arg = UnityVersionHandler.NewType().TypePointer;");
    };

    protected override IReadOnlyList<CodeGenProperty> WrapperProperties
    {
        get
        {
            CodeGenProperty byvalArg = new("INativeTypeStruct", ElementProtection.Public, "ByValArg")
            {
                ImmediateGet = "UnityVersionHandler.Wrap("
            };
            CodeGenProperty thisArg = new("INativeTypeStruct", ElementProtection.Public, "ThisArg")
            {
                ImmediateGet = "UnityVersionHandler.Wrap("
            };
            if (!ByValArgIsPointer)
                byvalArg.ImmediateGet += "(Il2CppTypeStruct*)&";
            if (!ThisArgIsPointer)
                thisArg.ImmediateGet += "(Il2CppTypeStruct*)&";
            byvalArg.ImmediateGet += "_->byval_arg)";
            thisArg.ImmediateGet += "_->this_arg)";
            return [
                new CodeGenProperty("VirtualInvokeData*", ElementProtection.Public, "VTable")
                {
                    ImmediateGet = $"(VirtualInvokeData*)nint.Add(Pointer, sizeof({NativeStructGenerator.NativeStruct.Name}))",
                    IsUnsafe = true
                },
                new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ClassPointer")
                {
                    ImmediateGet = $"({NativeStub}*)Pointer"
                },
                byvalArg,
                thisArg,
                new CodeGenProperty("bool", ElementProtection.Public, "HasCombinedInterfaceData")
                {
                    ImmediateGet = GetNativeField("interfaces") is not null ? "true" : "false"
                },
            ];
        }
    }

    protected override IReadOnlyList<BitfieldAccessor> BitfieldAccessors =>
    [
        new BitfieldAccessor("ValueType", "valuetype", defaultGetter: "ByValArg.ValueType && ThisArg.ValueType",
            defaultSetBuilder: writer =>
            {
                writer.WriteLine("ByValArg.ValueType = value;");
                writer.WriteLine("ThisArg.ValueType = value;");
            }),
        new BitfieldAccessor("Initialized", "initialized"),
        new BitfieldAccessor("EnumType", "enumtype"),
        new BitfieldAccessor("IsGeneric", "is_generic"),
        new BitfieldAccessor("HasReferences", "has_references"),
        new BitfieldAccessor("SizeInited", "size_inited"),
        new BitfieldAccessor("HasFinalize", "has_finalize"),
        new BitfieldAccessor("IsVtableInitialized", "is_vtable_initialized", defaultGetter: "false"),
        new BitfieldAccessor("InitializedAndNoError", "initialized_and_no_error", defaultGetter: "true")
    ];

    protected override IReadOnlyList<ByRefWrapper> ByRefWrappers =>
    [
        new ByRefWrapper("uint", "InstanceSize", ["instance_size"]),
        new ByRefWrapper("ushort", "VTableCount", ["vtable_count"]),
        new ByRefWrapper("ushort", "InterfaceCount", ["interfaces_count"]),
        new ByRefWrapper("ushort", "InterfaceOffsetsCount", ["interface_offsets_count"]),
        new ByRefWrapper("byte", "TypeHierarchyDepth", ["typeHierarchyDepth"]),
        new ByRefWrapper("int", "NativeSize", ["native_size"]),
        new ByRefWrapper("uint", "ActualSize", ["actualSize"]),
        new ByRefWrapper("ushort", "MethodCount", ["method_count"]),
        new ByRefWrapper("ushort", "FieldCount", ["field_count"]),
        new ByRefWrapper("Il2CppClassAttributes", "Flags", ["flags"]),
        new ByRefWrapper("nint", "Name", ["name"]),
        new ByRefWrapper("nint", "Namespace", ["namespaze"]),
        new ByRefWrapper("Il2CppImage*", "Image", ["image"]),
        new ByRefWrapper("Il2CppClass*", "Parent", ["parent"]),
        new ByRefWrapper("Il2CppClass*", "ElementClass", ["element_class"]),
        new ByRefWrapper("Il2CppClass*", "CastClass", ["castClass"]),
        new ByRefWrapper("Il2CppClass*", "DeclaringType", ["declaringType"]),
        new ByRefWrapper("Il2CppClass*", "Class", ["klass"], makeDummyIfNotExist: true),
        new ByRefWrapper("Il2CppFieldInfo*", "Fields", ["fields"]),
        new ByRefWrapper("Il2CppMethodInfo**", "Methods", ["methods"]),
        new ByRefWrapper("Il2CppClass**", "ImplementedInterfaces", ["implementedInterfaces"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("Il2CppRuntimeInterfaceOffsetPair*", "InterfaceOffsets", ["interfaceOffsets"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("Il2CppRuntimeInterfaceData*", "Interfaces", ["interfaces"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("Il2CppClass**", "TypeHierarchy", ["typeHierarchy"])
    ];
}
