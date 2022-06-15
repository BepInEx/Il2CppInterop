using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppEventInfoGenerator : VersionSpecificGenerator
{
    public Il2CppEventInfoGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeEventInfoStructHandler";
    protected override string HandlerInterface => "INativeEventInfoStructHandler";
    protected override string NativeInterface => "INativeEventInfoStruct";
    protected override string NativeStub => "Il2CppEventInfo";
    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "EventInfoPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }),
        new ByRefWrapper("Il2CppTypeStruct*", "EventType", new[] { "eventType" }),
        new ByRefWrapper("Il2CppClass*", "Parent", new[] { "parent" }),
        new ByRefWrapper("Il2CppMethodInfo*", "Add", new[] { "add" }),
        new ByRefWrapper("Il2CppMethodInfo*", "Remove", new[] { "remove" }),
        new ByRefWrapper("Il2CppMethodInfo*", "Raise", new[] { "raise" })
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
