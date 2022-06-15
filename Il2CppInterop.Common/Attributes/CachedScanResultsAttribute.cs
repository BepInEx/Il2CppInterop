namespace Il2CppInterop.Common.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CachedScanResultsAttribute : Attribute
{
    // Data for metadata init call
    public long MetadataInitFlagRva;
    public long MetadataInitTokenRva;
    public int RefRangeEnd;

    // Methods that call this method
    public int RefRangeStart;

    public int XrefRangeEnd;

    // Items that this method calls/uses
    public int XrefRangeStart;
}
