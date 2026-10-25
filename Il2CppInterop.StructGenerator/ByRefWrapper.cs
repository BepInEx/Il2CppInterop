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
