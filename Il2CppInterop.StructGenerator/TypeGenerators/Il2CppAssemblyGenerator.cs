using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppAssemblyGenerator : VersionSpecificGenerator
{
    public Il2CppAssemblyGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "Assembly";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "AssemblyPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        },
        new CodeGenProperty("INativeAssemblyNameStruct", ElementProtection.Public, "Name")
        {
            GetMethod = new CodeGenMethod("INativeAssemblyNameStruct", ElementProtection.Private, "get")
            {
                ImmediateReturn = "UnityVersionHandler.Wrap((Il2CppAssemblyName*)&_->aname)"
            },
            SetMethod = new CodeGenMethod("void", ElementProtection.Private, "set")
            {
                ImmediateReturn = $"_->aname = *({GetNativeField("aname")!.Type}*)Name.AssemblyNamePointer"
            }
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("Il2CppImage*", "Image", ["image"], addNotSupportedIfNotExist: true)
    ];
}
