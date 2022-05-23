using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppAssemblyNameGenerator : VersionSpecificGenerator
{
    public Il2CppAssemblyNameGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeAssemblyNameStructHandler";
    protected override string HandlerInterface => "INativeAssemblyNameStructHandler";
    protected override string NativeInterface => "INativeAssemblyNameStruct";
    protected override string NativeStub => "Il2CppAssemblyName";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "AssemblyNamePointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Name", new[] { "name", "nameIndex" }),
        new ByRefWrapper("IntPtr", "Culture", new[] { "culture", "cultureIndex" }),
        new ByRefWrapper("IntPtr", "PublicKey", new[] { "public_key", "publicKeyIndex" }),
        new ByRefWrapper("int", "Major", new[] { "major" }),
        new ByRefWrapper("int", "Minor", new[] { "minor" }),
        new ByRefWrapper("int", "Build", new[] { "build" }),
        new ByRefWrapper("int", "Revision", new[] { "revision" }),
        new ByRefWrapper("ulong", "PublicKeyToken", new[] { "public_key_token", "publicKeyToken" })
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
