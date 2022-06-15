using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppAssemblyGenerator : VersionSpecificGenerator
{
    public Il2CppAssemblyGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeAssemblyStructHandler";
    protected override string HandlerInterface => "INativeAssemblyStructHandler";
    protected override string NativeInterface => "INativeAssemblyStruct";
    protected override string NativeStub => "Il2CppAssembly";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "AssemblyPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" },
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
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("Il2CppImage*", "Image", new[] { "image" }, addNotSupportedIfNotExist: true)
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
