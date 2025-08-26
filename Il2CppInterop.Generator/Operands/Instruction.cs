namespace Il2CppInterop.Generator.Operands;

public sealed class Instruction : ILabel
{
    public OpCode Code { get; set; }
    public object? Operand { get; set; }

    public Instruction()
    {
    }

    public Instruction(OpCode code)
    {
        Code = code;
    }

    public Instruction(OpCode code, object? operand)
    {
        Code = code;
        Operand = operand;
    }
}
