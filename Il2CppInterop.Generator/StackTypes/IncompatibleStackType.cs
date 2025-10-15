namespace Il2CppInterop.Generator.StackTypes;

public sealed record class IncompatibleStackType : StackType
{
    public static IncompatibleStackType Instance { get; } = new();
    private IncompatibleStackType()
    {
    }

    public override string ToString() => "incompatible";
}
