using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppMethodInfoGenerator : VersionSpecificGenerator
{
    public Il2CppMethodInfoGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeMethodInfoStructHandler";
    protected override string HandlerInterface => "INativeMethodInfoStructHandler";
    protected override string NativeInterface => "INativeMethodInfoStruct";
    protected override string NativeStub => "Il2CppMethodInfo";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "MethodInfoPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("IntPtr", "Name", new[] { "name" }),
        new ByRefWrapper("ushort", "Slot", new[] { "slot" }),
        new ByRefWrapper("IntPtr", "MethodPointer", new[] { "methodPointer", "method" }),
        new ByRefWrapper("IntPtr", "VirtualMethodPointer", new[] { "virtualMethodPointer", "methodPointer", "method" }),
        new ByRefWrapper("Il2CppClass*", "Class", new[] { "declaring_type", "klass" }),
        new ByRefWrapper("IntPtr", "InvokerMethod", new[] { "invoker_method" }),
        new ByRefWrapper("Il2CppTypeStruct*", "ReturnType", new[] { "return_type" }),
        new ByRefWrapper("Il2CppMethodFlags", "Flags", new[] { "flags" }),
        new ByRefWrapper("byte", "ParametersCount", new[] { "parameters_count" }),
        new ByRefWrapper("Il2CppParameterInfo*", "Parameters", new[] { "parameters" }),
        new ByRefWrapper("uint", "Token", new[] { "token" }, addNotSupportedIfNotExist: true)
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => new()
    {
        new BitfieldAccessor("IsGeneric", "is_generic"),
        new BitfieldAccessor("IsInflated", "is_inflated"),
        new BitfieldAccessor("IsMarshalledFromNative", "is_marshaled_from_native", defaultGetter: "false")
    };
}
