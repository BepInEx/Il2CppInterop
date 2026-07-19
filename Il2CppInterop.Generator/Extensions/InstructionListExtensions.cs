using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator.Extensions;

internal static class InstructionListExtensions
{
    public static Instruction Add(this List<Instruction> instructions, CilOpCode opCode)
    {
        return instructions.Add(opCode, null);
    }

    public static Instruction Add(this List<Instruction> instructions, CilOpCode opCode, object? operand)
    {
        var instruction = new Instruction(opCode, operand);
        instructions.Add(instruction);
        return instruction;
    }
}
