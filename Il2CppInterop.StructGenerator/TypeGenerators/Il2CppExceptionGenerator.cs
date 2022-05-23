using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator.TypeGenerators;

internal class Il2CppExceptionGenerator : VersionSpecificGenerator
{
    public Il2CppExceptionGenerator(string metadataSuffix, CppClass nativeClass,
        Func<string, CppClass>? dependencyResolver = null) : base(metadataSuffix, nativeClass, dependencyResolver)
    {
    }

    protected override string HandlerName => "NativeExceptionStructHandler";
    protected override string HandlerInterface => "INativeExceptionStructHandler";
    protected override string NativeInterface => "INativeExceptionStruct";
    protected override string NativeStub => "Il2CppException";

    protected override List<CodeGenField>? WrapperFields => null;

    protected override List<CodeGenProperty>? WrapperProperties => new()
    {
        new CodeGenProperty($"{NativeStub}*", ElementProtection.Public, "ExceptionPointer")
        { ImmediateGet = $"({NativeStub}*)Pointer" }
    };

    protected override List<ByRefWrapper>? ByRefWrappers => new()
    {
        new ByRefWrapper("Il2CppException*", "InnerException", new[] { "inner_ex" }),
        new ByRefWrapper("Il2CppString*", "Message", new[] { "message" }),
        new ByRefWrapper("Il2CppString*", "HelpLink", new[] { "_helpURL", "help_link" }),
        new ByRefWrapper("Il2CppString*", "ClassName", new[] { "className", "class_name" }),
        new ByRefWrapper("Il2CppString*", "StackTrace", new[] { "stack_trace" }),
        new ByRefWrapper("Il2CppString*", "RemoteStackTrace", new[] { "remote_stack_trace" })
    };

    protected override List<BitfieldAccessor>? BitfieldAccessors => null;
}
