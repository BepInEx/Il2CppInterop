namespace Il2CppInterop.Generator.StackTypes;

public sealed record class IntegerStackTypeNative : IntegerStackType
{
    public static IntegerStackTypeNative Instance { get; } = new();
    private IntegerStackTypeNative()
    {
    }

    public override string ToString() => "nint";
}
