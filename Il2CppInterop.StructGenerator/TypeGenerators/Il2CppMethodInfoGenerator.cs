using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppMethodInfoGenerator : VersionSpecificGenerator
{
    public Il2CppMethodInfoGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "MethodInfo";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "MethodInfoPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("nint", "Name", ["name"]),
        new ByRefWrapper("ushort", "Slot", ["slot"]),
        new ByRefWrapper("nint", "MethodPointer", ["methodPointer", "method"]),
        new ByRefWrapper("nint", "VirtualMethodPointer", ["virtualMethodPointer", "methodPointer", "method"]),
        new ByRefWrapper("Il2CppClass*", "Class", ["declaring_type", "klass"]),
        new ByRefWrapper("nint", "InvokerMethod", ["invoker_method"]),
        new ByRefWrapper("Il2CppTypeStruct*", "ReturnType", ["return_type"]),
        new ByRefWrapper("Il2CppMethodFlags", "Flags", ["flags"]),
        new ByRefWrapper("byte", "ParametersCount", ["parameters_count"]),
        new ByRefWrapper("Il2CppParameterInfo*", "Parameters", ["parameters"]),
        new ByRefWrapper("uint", "Token", ["token"], addNotSupportedIfNotExist: true)
    ];

    protected override IReadOnlyList<BitfieldAccessor>? BitfieldAccessors =>
    [
        new BitfieldAccessor("IsGeneric", "is_generic"),
        new BitfieldAccessor("IsInflated", "is_inflated"),
        new BitfieldAccessor("IsMarshalledFromNative", "is_marshaled_from_native", defaultGetter: "false")
    ];
}
