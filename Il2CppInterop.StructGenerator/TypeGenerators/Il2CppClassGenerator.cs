using System.Text;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppClassGenerator : VersionSpecificGenerator
{
    public Il2CppClassGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
        var lastField = NativeStructGenerator.NativeStruct.Fields.Last();
        if (lastField.Name == "vtable") NativeStructGenerator.NativeStruct.Fields.Remove(lastField);
    }

    protected override string HandlerName => "NativeClassStructHandler";
    protected override string HandlerInterface => "INativeClassStructHandler";
    protected override string NativeInterface => "INativeClassStruct";
    protected override string NativeStub => "Il2CppClass";

    protected override List<CodeGenParameter>? CreateNewParameters =>
        new() { new CodeGenParameter("int", "vTableSlots") };

    protected override string? SizeOverride => "Size() + sizeof(VirtualInvokeData) * vTableSlots";
    protected override List<CodeGenField>? WrapperFields => null;
    private bool ByValArgIsPointer => GetNativeField("byval_arg")?.FieldType.EndsWith("*") ?? false;
    private bool ThisArgIsPointer => GetNativeField("this_arg")?.FieldType.EndsWith("*") ?? false;

    protected override Action<StringBuilder>? CreateNewExtraBody => builder =>
    {
        if (GetNativeField("vtable") is not null)
        {
            builder.AppendLine("Marshal.FreeHGlobal(ptr);");
            builder.AppendLine(
                $"throw new NotSupportedException(\"The native struct '{NativeStructGenerator.NativeStruct.Name}' has a vtable field which is not currently supported!\");");
            return;
        }

        if (ByValArgIsPointer) builder.AppendLine("_->byval_arg = UnityVersionHandler.NewType().TypePointer;");
        if (ThisArgIsPointer) builder.AppendLine("_->this_arg = UnityVersionHandler.NewType().TypePointer;");
    };

    protected override List<CodeGenProperty>? WrapperProperties
    {
        get
        {
            List<CodeGenProperty> properties = new()
            {
                new CodeGenProperty("IntPtr", ElementProtection.Public, "VTable")
                {
                    ImmediateGet = $"IntPtr.Add(Pointer, sizeof({NativeStructGenerator.NativeStruct.Name}))"
                },
                new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ClassPointer")
                { ImmediateGet = $"({NativeStub}*)Pointer" }
            };
            CodeGenProperty byvalArg = new("INativeTypeStruct", ElementProtection.Public, "ByValArg")
            { ImmediateGet = "UnityVersionHandler.Wrap(" };
            CodeGenProperty thisArg = new("INativeTypeStruct", ElementProtection.Public, "ThisArg")
            { ImmediateGet = "UnityVersionHandler.Wrap(" };
            AddExtraUsing("Il2CppInterop.Runtime.Runtime.VersionSpecific.Type");
            if (!ByValArgIsPointer) byvalArg.ImmediateGet += "(Il2CppTypeStruct*)&";
            if (!ThisArgIsPointer) thisArg.ImmediateGet += "(Il2CppTypeStruct*)&";
            byvalArg.ImmediateGet += "_->byval_arg)";
            thisArg.ImmediateGet += "_->this_arg)";
            properties.Add(byvalArg);
            properties.Add(thisArg);
            return properties;
        }
    }

    protected override List<BitfieldAccessor>? BitfieldAccessors => new()
    {
        new BitfieldAccessor("ValueType", "valuetype", defaultGetter: "ByValArg.ValueType && ThisArg.ValueType",
            defaultSetBuilder: builder =>
            {
                builder.AppendLine("ByValArg.ValueType = value;");
                builder.Append("ThisArg.ValueType = value;");
            }),
        new BitfieldAccessor("Initialized", "initialized"),
        new BitfieldAccessor("EnumType", "enumtype"),
        new BitfieldAccessor("IsGeneric", "is_generic"),
        new BitfieldAccessor("HasReferences", "has_references"),
        new BitfieldAccessor("SizeInited", "size_inited"),
        new BitfieldAccessor("HasFinalize", "has_finalize"),
        new BitfieldAccessor("IsVtableInitialized", "is_vtable_initialized", defaultGetter: "false"),
        new BitfieldAccessor("InitializedAndNoError", "initialized_and_no_error", defaultGetter: "true")
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("uint", "InstanceSize", new[] { "instance_size" }),
        new ByRefWrapper("ushort", "VtableCount", new[] { "vtable_count" }),
        new ByRefWrapper("ushort", "InterfaceCount", new[] { "interfaces_count" }),
        new ByRefWrapper("ushort", "InterfaceOffsetsCount", new[] { "interface_offsets_count" }),
        new ByRefWrapper("byte", "TypeHierarchyDepth", new[] { "typeHierarchyDepth" }),
        new ByRefWrapper("int", "NativeSize", new[] { "native_size" }),
        new ByRefWrapper("uint", "ActualSize", new[] { "actualSize" }),
        new ByRefWrapper("ushort", "MethodCount", new[] { "method_count" }),
        new ByRefWrapper("ushort", "FieldCount", new[] { "field_count" }),
        new ByRefWrapper("Il2CppClassAttributes", "Flags", new[] { "flags" }),
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }),
        new ByRefWrapper("IntPtr", "Namespace", new[] { "namespaze" }),
        new ByRefWrapper("Il2CppImage*", "Image", new[] { "image" }),
        new ByRefWrapper("Il2CppClass*", "Parent", new[] { "parent" }),
        new ByRefWrapper("Il2CppClass*", "ElementClass", new[] { "element_class" }),
        new ByRefWrapper("Il2CppClass*", "CastClass", new[] { "castClass" }),
        new ByRefWrapper("Il2CppClass*", "DeclaringType", new[] { "declaringType" }),
        new ByRefWrapper("Il2CppClass*", "Class", new[] { "klass" }, makeDummyIfNotExist: true),
        new ByRefWrapper("Il2CppFieldInfo*", "Fields", new[] { "fields" }),
        new ByRefWrapper("Il2CppMethodInfo**", "Methods", new[] { "methods" }),
        new ByRefWrapper("Il2CppClass**", "ImplementedInterfaces", new[] { "implementedInterfaces" }),
        new ByRefWrapper("Il2CppRuntimeInterfaceOffsetPair*", "InterfaceOffsets", new[] { "interfaceOffsets" }),
        new ByRefWrapper("Il2CppClass**", "TypeHierarchy", new[] { "typeHierarchy" })
    };
}
