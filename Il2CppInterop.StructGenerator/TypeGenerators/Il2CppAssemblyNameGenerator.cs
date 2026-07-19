using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppAssemblyNameGenerator : VersionSpecificGenerator
{
    public Il2CppAssemblyNameGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "AssemblyName";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "AssemblyNamePointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("nint", "Name", ["name", "nameIndex"]),
        new ByRefWrapper("nint", "Culture", ["culture", "cultureIndex"]),
        new ByRefWrapper("nint", "PublicKey", ["public_key", "publicKeyIndex"]),
        new ByRefWrapper("int", "Major", ["major"]),
        new ByRefWrapper("int", "Minor", ["minor"]),
        new ByRefWrapper("int", "Build", ["build"]),
        new ByRefWrapper("int", "Revision", ["revision"]),
        new ByRefWrapper("ulong", "PublicKeyToken", ["public_key_token", "publicKeyToken"])
    ];
}
