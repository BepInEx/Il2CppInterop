using System.CodeDom.Compiler;
using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator;

internal class StructHandlerGenerator
{
    public StructHandlerGenerator(string name, string handlerInterface, string nativeInterface, string nativeStub,
        NativeStructGenerator nativeStructGen, IEnumerable<CodeGenParameter>? parameterOverride = null,
        Action<IndentedTextWriter>? extraBodyProvider = null)
    {
        NativeGenerator = nativeStructGen;
        HandlerClass = new CodeGenClass(ElementProtection.Public, name)
        {
            IsUnsafe = true,
            InterfaceNames = { handlerInterface }
        };
        HandlerClass.Properties.Add(new CodeGenProperty(name, ElementProtection.Public, "Instance")
        {
            EmptyGet = true,
            IsStatic = true,
            Initializer = "new()"
        });
        HandlerClass.Properties.Add(new CodeGenProperty("int", ElementProtection.Public, "Size")
        {
            ImmediateGet = $"sizeof({nativeStructGen.NativeStruct.Name})"
        });
        HandlerClass.Methods.Add(new CodeGenConstructor(name, ElementProtection.Private));
        CodeGenMethod createNewMethod = new(nativeInterface, ElementProtection.Public, "CreateNewStruct")
        {
            MethodBodyBuilder = writer =>
            {
                writer.Write("nint ptr = Marshal.AllocHGlobal(");
                if (SizeProviderOverride != null)
                    writer.WriteLine($"{SizeProviderOverride});");
                else
                    writer.WriteLine("Size);");
                writer.WriteLine(
                    $"{nativeStructGen.NativeStruct.Name}* _ = ({nativeStructGen.NativeStruct.Name}*)ptr;");
                writer.WriteLine("*_ = default;");
                extraBodyProvider?.Invoke(writer);
                writer.WriteLine("return new NativeStructWrapper(ptr);");
            }
        };
        if (parameterOverride != null)
            createNewMethod.Parameters.AddRange(parameterOverride);
        HandlerClass.Methods.Add(createNewMethod);
        HandlerClass.Methods.Add(new CodeGenMethod(nativeInterface, ElementProtection.Public, "Wrap")
        {
            Parameters = { new CodeGenParameter($"{nativeStub}*", "ptr") },
            MethodBodyBuilder = writer =>
            {
                writer.WriteLine("if (ptr == null) return null;");
                writer.WriteLine("return new NativeStructWrapper((nint)ptr);");
            }
        });
    }

    public CodeGenClass HandlerClass { get; }
    public NativeStructGenerator NativeGenerator { get; }
    public string? SizeProviderOverride { get; init; }
}
