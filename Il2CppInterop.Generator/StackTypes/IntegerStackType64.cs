namespace Il2CppInterop.Generator.StackTypes;

public sealed record class IntegerStackType64 : IntegerStackType
{
    public static IntegerStackType64 Instance { get; } = new();
    private IntegerStackType64()
    {
    }

    public override string ToString() => "long";
}
