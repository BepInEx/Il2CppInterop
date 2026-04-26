namespace Il2CppInterop.Generator.StackTypes;

public sealed record class UnknownStackType : StackType
{
    public static UnknownStackType Instance { get; } = new();
    private UnknownStackType()
    {
    }

    public override string ToString() => "unknown";
}
