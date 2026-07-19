namespace Il2CppInterop.Generator.StackTypes;

public sealed record class SingleStackType : StackType
{
    public static SingleStackType Instance { get; } = new();
    private SingleStackType()
    {
    }
    public override string ToString() => "float";
}
