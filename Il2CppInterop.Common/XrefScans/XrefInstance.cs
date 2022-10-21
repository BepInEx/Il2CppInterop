namespace Il2CppInterop.Common.XrefScans;

public readonly struct XrefInstance
{
    public readonly XrefType Type;
    public readonly nint Pointer;
    public readonly nint FoundAt;

    public XrefInstance(XrefType type, nint pointer, nint foundAt)
    {
        Type = type;
        Pointer = pointer;
        FoundAt = foundAt;
    }

    internal XrefInstance RelativeToBase(nint baseAddress)
    {
        return new XrefInstance(Type, Pointer - baseAddress, FoundAt - baseAddress);
    }
}
