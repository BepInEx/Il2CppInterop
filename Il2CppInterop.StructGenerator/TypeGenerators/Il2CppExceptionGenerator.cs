using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppExceptionGenerator : VersionSpecificGenerator
{
    public Il2CppExceptionGenerator(string metadataSuffix, CppClass nativeClass) : base(metadataSuffix, nativeClass)
    {
    }

    public override string GeneratorName => "Exception";

    protected override IReadOnlyList<CodeGenProperty>? WrapperProperties =>
    [
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ExceptionPointer")
        {
            ImmediateGet = $"({NativeStub}*)Pointer"
        }
    ];

    protected override IReadOnlyList<ByRefWrapper>? ByRefWrappers =>
    [
        new ByRefWrapper("Il2CppException*", "InnerException", ["inner_ex"]),
        new ByRefWrapper("Il2CppString*", "Message", ["message"]),
        new ByRefWrapper("Il2CppString*", "HelpLink", ["_helpURL", "help_link"]),
        new ByRefWrapper("Il2CppString*", "ClassName", ["className", "class_name"]),
        new ByRefWrapper("Il2CppString*", "StackTrace", ["stack_trace"]),
        new ByRefWrapper("Il2CppString*", "RemoteStackTrace", ["remote_stack_trace"])
    ];
}
