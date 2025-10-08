using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;

namespace Il2CppInterop.Generator.Extensions;

// https://github.com/Washi1337/AsmResolver/issues/678
// https://github.com/Washi1337/AsmResolver/pull/679
internal static class CilInstructionCollectionBackport
{
    extension (CilInstructionCollection instructions)
    {
        public void ExpandMacrosBackport()
        {
            int currentOffset = 0;
            foreach (var instruction in instructions)
            {
                instruction.Offset = currentOffset;
                instructions.ExpandMacroBackport(instruction);
                currentOffset += instruction.Size;
            }
        }
        private void ExpandMacroBackport(CilInstruction instruction)
        {
            // operand changes must come before opcode changes to not overwrite needed data
            switch (instruction.OpCode.Code)
            {
                case CilCode.Ldc_I4_0:
                case CilCode.Ldc_I4_1:
                case CilCode.Ldc_I4_2:
                case CilCode.Ldc_I4_3:
                case CilCode.Ldc_I4_4:
                case CilCode.Ldc_I4_5:
                case CilCode.Ldc_I4_6:
                case CilCode.Ldc_I4_7:
                case CilCode.Ldc_I4_8:
                case CilCode.Ldc_I4_M1:
                case CilCode.Ldc_I4_S:
                    instruction.Operand = instruction.GetLdcI4Constant();
                    instruction.OpCode = CilOpCodes.Ldc_I4;
                    break;

                case CilCode.Ldarg_0:
                case CilCode.Ldarg_1:
                case CilCode.Ldarg_2:
                case CilCode.Ldarg_3:
                case CilCode.Ldarg_S:
                    instruction.Operand = instructions.Owner.Owner?.Parameters is { } parameters
                        ? instruction.GetParameter(parameters)
                        : (ushort)instruction.GetParameterIndex();
                    instruction.OpCode = CilOpCodes.Ldarg;
                    break;

                case CilCode.Ldarga_S:
                    {
                        if (instruction.Operand is byte index)
                            instruction.Operand = (ushort)index;
                        instruction.OpCode = CilOpCodes.Ldarga;
                        break;
                    }

                case CilCode.Starg_S:
                    {
                        if (instruction.Operand is byte index)
                            instruction.Operand = (ushort)index;
                        instruction.OpCode = CilOpCodes.Starg;
                        break;
                    }

                case CilCode.Ldloc_0:
                case CilCode.Ldloc_1:
                case CilCode.Ldloc_2:
                case CilCode.Ldloc_3:
                case CilCode.Ldloc_S:
                    instruction.Operand = instruction.GetLocalVariable(instructions.Owner.LocalVariables);
                    instruction.OpCode = CilOpCodes.Ldloc;
                    break;

                case CilCode.Ldloca_S:
                    {
                        if (instruction.Operand is byte index)
                            instruction.Operand = (ushort)index;
                        instruction.OpCode = CilOpCodes.Ldloca;
                        break;
                    }

                case CilCode.Stloc_0:
                case CilCode.Stloc_1:
                case CilCode.Stloc_2:
                case CilCode.Stloc_3:
                case CilCode.Stloc_S:
                    instruction.Operand = instruction.GetLocalVariable(instructions.Owner.LocalVariables);
                    instruction.OpCode = CilOpCodes.Stloc;
                    break;

                case CilCode.Beq_S:
                    instruction.OpCode = CilOpCodes.Beq;
                    break;
                case CilCode.Bge_S:
                    instruction.OpCode = CilOpCodes.Bge;
                    break;
                case CilCode.Bgt_S:
                    instruction.OpCode = CilOpCodes.Bgt;
                    break;
                case CilCode.Ble_S:
                    instruction.OpCode = CilOpCodes.Ble;
                    break;
                case CilCode.Blt_S:
                    instruction.OpCode = CilOpCodes.Blt;
                    break;
                case CilCode.Br_S:
                    instruction.OpCode = CilOpCodes.Br;
                    break;
                case CilCode.Brfalse_S:
                    instruction.OpCode = CilOpCodes.Brfalse;
                    break;
                case CilCode.Brtrue_S:
                    instruction.OpCode = CilOpCodes.Brtrue;
                    break;
                case CilCode.Bge_Un_S:
                    instruction.OpCode = CilOpCodes.Bge_Un;
                    break;
                case CilCode.Bgt_Un_S:
                    instruction.OpCode = CilOpCodes.Bgt_Un;
                    break;
                case CilCode.Ble_Un_S:
                    instruction.OpCode = CilOpCodes.Ble_Un;
                    break;
                case CilCode.Blt_Un_S:
                    instruction.OpCode = CilOpCodes.Blt_Un;
                    break;
                case CilCode.Bne_Un_S:
                    instruction.OpCode = CilOpCodes.Bne_Un;
                    break;
                case CilCode.Leave_S:
                    instruction.OpCode = CilOpCodes.Leave;
                    break;
            }
        }
    }
}
