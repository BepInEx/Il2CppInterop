using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Utils;

internal static class InstructionExtensions
{
    public static bool IsStelem(this Code opCode)
    {
        switch (opCode)
        {
            case Code.Stelem_I:
            case Code.Stelem_I1:
            case Code.Stelem_I2:
            case Code.Stelem_I4:
            case Code.Stelem_I8:
            case Code.Stelem_R4:
            case Code.Stelem_R8:
            case Code.Stelem_Ref:
                return true;
            default: return false;
        }
    }

    public static bool IsLdelem(this Code opCode)
    {
        switch (opCode)
        {
            case Code.Ldelem_I:
            case Code.Ldelem_I1:
            case Code.Ldelem_I2:
            case Code.Ldelem_I4:
            case Code.Ldelem_I8:
            case Code.Ldelem_R4:
            case Code.Ldelem_R8:
            case Code.Ldelem_Ref:
                return true;
            default: return false;
        }
    }

    public static bool IsLdind(this Code opCode)
    {
        switch (opCode)
        {
            case Code.Ldind_I:
            case Code.Ldind_I1:
            case Code.Ldind_I2:
            case Code.Ldind_I4:
            case Code.Ldind_I8:
            case Code.Ldind_R4:
            case Code.Ldind_R8:
            case Code.Ldind_Ref:
            case Code.Ldind_U1:
            case Code.Ldind_U2:
            case Code.Ldind_U4:
                return true;
            default: return false;
        }
    }

    public static bool BreaksFlow(this OpCode opCode)
    {
        if (opCode == OpCodes.Jmp) return true;
        switch (opCode.FlowControl)
        {
            case FlowControl.Return:
            case FlowControl.Branch:
            case FlowControl.Cond_Branch:
                return true;
            default: return false;
        }
    }

    public static int PushAmount(this Instruction ins)
    {
        return ins.OpCode.StackBehaviourPush switch
        {
            StackBehaviour.Push0 => 0,
            StackBehaviour.Varpush => ((MethodReference)ins.Operand)
                .ReturnType.FullName == "System.Void" ? 0 : 1,
            StackBehaviour.Push1_push1 => 2,
            _ => 1,
        };
    }

    public static int PopAmount(this Instruction ins)
    {
        return ins.OpCode.StackBehaviourPop switch
        {
            StackBehaviour.Pop0 => 0,
            StackBehaviour.Pop1 => 1,
            StackBehaviour.Popi => 1,
            StackBehaviour.Popref => 1,
            StackBehaviour.Pop1_pop1 => 2,
            StackBehaviour.Popi_pop1 => 2,
            StackBehaviour.Popi_popi => 2,
            StackBehaviour.Popi_popi8 => 2,
            StackBehaviour.Popi_popi_popi => 3,
            StackBehaviour.Popi_popr4 => 2,
            StackBehaviour.Popi_popr8 => 2,
            StackBehaviour.Popref_pop1 => 2,
            StackBehaviour.Popref_popi => 2,
            StackBehaviour.Popref_popi_popi => 3,
            StackBehaviour.Popref_popi_popi8 => 3,
            StackBehaviour.Popref_popi_popr4 => 3,
            StackBehaviour.Popref_popi_popr8 => 3,
            StackBehaviour.Popref_popi_popref => 3,
            StackBehaviour.Varpop => GetParameterCount(ins),
            var pop => throw new NotSupportedException(
                $"{Enum.GetName(typeof(StackBehaviour), pop)} is not a pop behaviour"),
        };
    }

    public static int GetParameterCount(this Instruction ins)
    {
        if (ins.Operand is not MethodReference method)
            throw new ArgumentException("Operand must be a method", nameof(ins));
        if (method.HasThis && ins.OpCode.Code != Code.Newobj)
            return method.Parameters.Count + 1;
        return method.Parameters.Count;
    }

    public static bool TryGetLdlocIndex(this Instruction ins, out int index)
    {
        index = ins.OpCode.Code switch
        {
            Code.Ldloc_0 => 0,
            Code.Ldloc_1 => 1,
            Code.Ldloc_2 => 2,
            Code.Ldloc_3 => 3,
            Code.Ldloc or
            Code.Ldloc_S => ((VariableReference)ins.Operand).Index,
            _ => -1,
        };
        return index >= 0;
    }

    public static bool TryGetLdargIndex(this Instruction ins, bool hasThis, out int index)
    {
        var thisOffset = hasThis ? -1 : 0;
        index = ins.OpCode.Code switch
        {
            Code.Ldarg_0 => thisOffset + 0,
            Code.Ldarg_1 => thisOffset + 1,
            Code.Ldarg_2 => thisOffset + 2,
            Code.Ldarg_3 => thisOffset + 3,
            Code.Ldarg or
            Code.Ldarg_S => ((ParameterReference)ins.Operand).Index,
            _ => -2,
        };
        return index >= -1;
    }

    public static OpCode GetLong(this OpCode opCode)
    {
        return opCode.OperandType switch
        {
            OperandType.ShortInlineArg => throw new NotImplementedException(opCode.OperandType.ToString()),
            OperandType.ShortInlineBrTarget => opCode.Code switch
            {
                Code.Br_S => OpCodes.Br,
                Code.Brfalse_S => OpCodes.Brfalse,
                Code.Brtrue_S => OpCodes.Brtrue,
                Code.Beq_S => OpCodes.Beq,
                Code.Bge_S => OpCodes.Bge,
                Code.Bgt_S => OpCodes.Bgt,
                Code.Ble_S => OpCodes.Ble,
                Code.Blt_S => OpCodes.Blt,
                Code.Bne_Un_S => OpCodes.Bne_Un,
                Code.Bge_Un_S => OpCodes.Bge_Un,
                Code.Bgt_Un_S => OpCodes.Bgt_Un,
                Code.Ble_Un_S => OpCodes.Ble_Un,
                Code.Blt_Un_S => OpCodes.Blt_Un,
                Code.Leave_S => OpCodes.Leave,
                _ => throw new NotImplementedException($"{opCode.OperandType} {opCode.Code}"),
            },
            OperandType.ShortInlineI => throw new NotImplementedException(opCode.OperandType.ToString()),
            OperandType.ShortInlineR => throw new NotImplementedException(opCode.OperandType.ToString()),
            OperandType.ShortInlineVar => throw new NotImplementedException(opCode.OperandType.ToString()),
            _ => throw new NotSupportedException($"{opCode.OperandType} is not a short version OpCode"),
        };
    }

}
