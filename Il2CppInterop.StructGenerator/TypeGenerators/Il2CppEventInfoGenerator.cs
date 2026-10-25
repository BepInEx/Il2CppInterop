using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppEventInfoGenerator : VersionSpecificGenerator
{
    public Il2CppEventInfoGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "EventInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "EventInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("nint", "Name", ["name"]),
        new ByRefWrapper("Il2CppTypeStruct*", "EventType", ["eventType"]),
        new ByRefWrapper("Il2CppClass*", "Parent", ["parent"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Add", ["add"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Remove", ["remove"]),
        new ByRefWrapper("Il2CppMethodInfo*", "Raise", ["raise"])
    ];
}
