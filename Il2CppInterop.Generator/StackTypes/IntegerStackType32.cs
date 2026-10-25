namespace Il2CppInterop.Generator.StackTypes;

public sealed record class IntegerStackType32 : IntegerStackType
{
    public static IntegerStackType32 Instance { get; } = new();
    private IntegerStackType32()
    {
    }

    public override string ToString() => "int";
}
