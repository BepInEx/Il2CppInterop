using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppFieldInfoGenerator : VersionSpecificGenerator
{
    public Il2CppFieldInfoGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeFieldInfoStructHandler";
    protected override string HandlerInterface => "INativeFieldInfoStructHandler";
    protected override string NativeInterface => "INativeFieldInfoStruct";
    protected override string NativeStub => "Il2CppFieldInfo";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "FieldInfoPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }),
        new ByRefWrapper("Il2CppTypeStruct*", "Type", new[] { "type" }),
        new ByRefWrapper("Il2CppClass*", "Parent", new[] { "parent" }),
        new ByRefWrapper("int", "Offset", new[] { "offset" })
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
