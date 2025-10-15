namespace Il2CppInterop.Generator.StackTypes;

public sealed record class IntegerStackTypeNative : StackType
{
    public static IntegerStackTypeNative Instance { get; } = new();
    private IntegerStackTypeNative()
    {
    }

    public override string ToString() => "nint";
}
