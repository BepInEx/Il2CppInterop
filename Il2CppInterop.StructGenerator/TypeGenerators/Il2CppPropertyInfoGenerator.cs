using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppPropertyInfoGenerator : VersionSpecificGenerator
{
    public Il2CppPropertyInfoGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativePropertyInfoStructHandler";
    protected override string HandlerInterface => "INativePropertyInfoStructHandler";
    protected override string NativeInterface => "INativePropertyInfoStruct";
    protected override string NativeStub => "Il2CppPropertyInfo";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "PropertyInfoPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }),
        new ByRefWrapper("Il2CppClass*", "Parent", new[] { "parent" }),
        new ByRefWrapper("Il2CppMethodInfo*", "Get", new[] { "get" }),
        new ByRefWrapper("Il2CppMethodInfo*", "Set", new[] { "set" }),
        new ByRefWrapper("uint", "Attrs", new[] { "attrs" })
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
