using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppFieldInfoGenerator : VersionSpecificGenerator
{
    public Il2CppFieldInfoGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "FieldInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "FieldInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("nint", "Name", ["name"]),
        new ByRefWrapper("Il2CppTypeStruct*", "Type", ["type"]),
        new ByRefWrapper("Il2CppClass*", "Parent", ["parent"]),
        new ByRefWrapper("int", "Offset", ["offset"])
    ];
}
