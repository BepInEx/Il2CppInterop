using System.Text;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator;

internal class StructHandlerGenerator
{
    public StructHandlerGenerator(string name, string handlerInterface, string nativeInterface, string nativeStub,
        NativeStructGenerator nativeStructGen, List<CodeGenParameter>? parameterOverride = null,
        Action<StringBuilder>? extraBodyProvider = null)
    {
        NativeGenerator = nativeStructGen;
        HandlerClass = new CodeGenClass(ElementProtection.Public, name)
        {
            IsUnsafe = true,
            InterfaceNames = { handlerInterface }
        };
        HandlerClass.Methods.Add(new CodeGenMethod("int", ElementProtection.Public, "Size")
        {
            ImmediateReturn = $"sizeof({nativeStructGen.NativeStruct.Name})"
        });
        CodeGenMethod createNewMethod = new(nativeInterface, ElementProtection.Public, "CreateNewStruct")
        {
            MethodBodyBuilder = builder =>
            {
                builder.Append("IntPtr ptr = Marshal.AllocHGlobal(");
                if (SizeProviderOverride != null) builder.AppendLine($"{SizeProviderOverride});");
                else builder.AppendLine("Size());");
                builder.AppendLine(
                    $"{nativeStructGen.NativeStruct.Name}* _ = ({nativeStructGen.NativeStruct.Name}*)ptr;");
                builder.AppendLine("*_ = default;");
                if (extraBodyProvider != null)
                    extraBodyProvider(builder);
                builder.Append("return new NativeStructWrapper(ptr);");
            }
        };
        if (parameterOverride != null)
            createNewMethod.Parameters.AddRange(parameterOverride);
        HandlerClass.Methods.Add(createNewMethod);
        HandlerClass.Methods.Add(new CodeGenMethod(nativeInterface, ElementProtection.Public, "Wrap")
        {
            Parameters = { new CodeGenParameter($"{nativeStub}*", "ptr") },
            MethodBodyBuilder = builder =>
            {
                builder.AppendLine("if (ptr == null) return null;");
                builder.Append("return new NativeStructWrapper((IntPtr)ptr);");
            }
        });
    }

    public CodeGenClass HandlerClass { get; }
    public NativeStructGenerator NativeGenerator { get; }
    public string? SizeProviderOverride { get; init; }
}
