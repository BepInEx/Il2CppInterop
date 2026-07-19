using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppParameterInfoGenerator : VersionSpecificGenerator
{
    public Il2CppParameterInfoGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "ParameterInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ParameterInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        },
        new CodeGenProperty("bool", ElementProtection.Public, "HasNamePosToken")
        {
            ImmediateGet = GetNativeField("name") is not null ? "true" : "false"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("nint", "Name", ["name"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("int", "Position", ["position"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("uint", "Token", ["token"], addNotSupportedIfNotExist: true),
        new ByRefWrapper("Il2CppTypeStruct*", "ParameterType", ["parameter_type"]),
    ];
}
