using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppTypeGenerator : VersionSpecificGenerator
{
    public Il2CppTypeGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeTypeStructHandler";
    protected override string HandlerInterface => "INativeTypeStructHandler";
    protected override string NativeInterface => "INativeTypeStruct";
    protected override string NativeStub => "Il2CppTypeStruct";
    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "TypePointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Data", new[] { "data" }),
        new ByRefWrapper("ushort", "Attrs", new[] { "attrs" }),
        new ByRefWrapper("Il2CppTypeEnum", "Type", new[] { "type" })
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => new()
    {
        new BitfieldAccessor("ByRef", "byref"),
        new BitfieldAccessor("Pinned", "pinned"),
        // maybe throw if not exist
        new BitfieldAccessor("ValueType", "valuetype", defaultGetter: "false")
    };
}
