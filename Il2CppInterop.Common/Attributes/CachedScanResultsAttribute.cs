namespace Il2CppInterop.Common.Attributes;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public class CachedScanResultsAttribute : Attribute
{
    // Data for metadata init call
    public long MetadataInitFlagRva { get; set; }
    public long[] MetadataInitTokenRvas { get; set; }
    public int RefRangeEnd { get; set; }

    // Methods that call this method
    public int RefRangeStart { get; set; }

    public int XrefRangeEnd { get; set; }

    // Items that this method calls/uses
    public int XrefRangeStart { get; set; }
}
