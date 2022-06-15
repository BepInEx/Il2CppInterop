using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.CodeGen.Enums;

namespace Il2CppInterop.StructGenerator;

internal class ByRefWrapper
{
    public ByRefWrapper(string wrappedType, string wrappedName, string[] nativeNames, string? forcedNativeType = null,
        bool addNotSupportedIfNotExist = false, bool makeDummyIfNotExist = false)
    {
        WrappedType = wrappedType;
        WrappedName = wrappedName;
        NativeNames = nativeNames;
        ForcedNativeType = forcedNativeType;
        AddNotSupported = addNotSupportedIfNotExist;
        MakeDummyIfNotSupported = makeDummyIfNotExist;
    }

    public string WrappedType { get; }
    public string WrappedName { get; }
    public string[] NativeNames { get; }
    public string? ForcedNativeType { get; }
    public bool AddNotSupported { get; }
    public bool MakeDummyIfNotSupported { get; }
}

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
            Parameters = { new CodeGenParameter("IntPtr", "ptr") },
            ImmediateReturn = "Pointer = ptr"
        });

        WrapperClass.Properties.Add(new CodeGenProperty("IntPtr", ElementProtection.Public, "Pointer")
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
