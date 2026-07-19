using Il2CppInterop.StructGenerator.CodeGen;

namespace Il2CppInterop.StructGenerator;

internal class StructWrapperGenerator
{
    public StructWrapperGenerator(string nativeInterface)
    {
        WrapperClass = new CodeGenClass(ElementProtection.Internal, "NativeStructWrapper")
        {
            InterfaceNames = { nativeInterface }
        };
        WrapperClass.Methods.Add(new CodeGenConstructor("NativeStructWrapper", ElementProtection.Public)
        {
            Parameters = { new CodeGenParameter("nint", "ptr") },
            ImmediateReturn = "Pointer = ptr"
        });

        WrapperClass.Properties.Add(new CodeGenProperty("nint", ElementProtection.Public, "Pointer")
        {
            EmptyGet = true
        });
    }

    public CodeGenClass WrapperClass { get; }

    public void ImplementProperties(List<CodeGenProperty> properties)
    {
        WrapperClass.Properties.AddRange(properties);
    }
}
