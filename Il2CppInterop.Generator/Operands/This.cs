namespace Il2CppInterop.Generator.Operands;

public sealed record class This
{
    public static This Instance { get; } = new This();
    private This()
    {
    }
}
