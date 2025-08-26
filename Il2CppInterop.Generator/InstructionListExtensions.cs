using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

internal static class InstructionListExtensions
{
    public static void Add(this List<Instruction> instructions, OpCode opCode)
    {
        instructions.Add(new Instruction(opCode));
    }

    public static void Add(this List<Instruction> instructions, OpCode opCode, object? operand)
    {
        instructions.Add(new Instruction(opCode, operand));
    }
}
