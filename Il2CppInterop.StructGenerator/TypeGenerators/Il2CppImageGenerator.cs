using System.Text;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppImageGenerator : VersionSpecificGenerator
{
    public Il2CppImageGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeImageStructHandler";
    protected override string HandlerInterface => "INativeImageStructHandler";
    protected override string NativeInterface => "INativeImageStruct";
    protected override string NativeStub => "Il2CppImage";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ImagePointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" },
        new CodeGenProperty("bool", ElementProtection.Public, "HasNameNoExt")
        { ImmediateGet = GetNativeField("nameNoExt") is not null ? "true" : "false" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("Il2CppAssembly*", "Assembly", new[] { "assembly" }, addNotSupportedIfNotExist: true),
        new ByRefWrapper("byte", "Dynamic", new[] { "dynamic" }, makeDummyIfNotExist: true),
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }),
        new ByRefWrapper("IntPtr", "NameNoExt", new[] { "nameNoExt" }, addNotSupportedIfNotExist: true)
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;

    protected override Action<StringBuilder>? CreateNewExtraBody => builder =>
    {
        if (GetNativeField("metadataHandle") is not null)
        {
            builder.AppendLine(
                "Il2CppImageGlobalMetadata* metadata = (Il2CppImageGlobalMetadata*)Marshal.AllocHGlobal(sizeof(Il2CppImageGlobalMetadata));");
            builder.AppendLine("metadata->image = (Il2CppImage*)_;");
            builder.AppendLine("*(Il2CppImageGlobalMetadata**)&_->metadataHandle = metadata;");
        }
    };
}
