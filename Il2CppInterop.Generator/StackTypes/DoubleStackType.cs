namespace Il2CppInterop.Generator.StackTypes;

public sealed record class DoubleStackType : StackType
{
    public static DoubleStackType Instance { get; } = new();
    private DoubleStackType()
    {
    }
    public override string ToString() => "double";
}
