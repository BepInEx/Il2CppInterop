using AsmResolver.PE.DotNet.Cil;
using Cpp2IL.Core.Model.Contexts;

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

    public override string ToString() => $"{Code} {Operand}";

    public int GetPopCount(MethodAnalysisContext owner)
    {
        return Code.StackBehaviourPop switch
        {
            CilStackBehaviour.Pop0 => 0,
            CilStackBehaviour.Pop1 or CilStackBehaviour.PopI or CilStackBehaviour.PopRef => 1,
            CilStackBehaviour.Pop1_Pop1 or CilStackBehaviour.PopI_Pop1 or CilStackBehaviour.PopI_PopI or CilStackBehaviour.PopI_PopI8 or CilStackBehaviour.PopI_PopR4 or CilStackBehaviour.PopI_PopR8 or CilStackBehaviour.PopRef_Pop1 or CilStackBehaviour.PopRef_PopI => 2,
            CilStackBehaviour.PopRef_PopI_Pop1 or CilStackBehaviour.PopI_PopI_PopI or CilStackBehaviour.PopRef_PopI_PopI or CilStackBehaviour.PopRef_PopI_PopI8 or CilStackBehaviour.PopRef_PopI_PopR4 or CilStackBehaviour.PopRef_PopI_PopR8 or CilStackBehaviour.PopRef_PopI_PopRef => 3,
            CilStackBehaviour.VarPop => Code.Code switch
            {
                CilCode.Ret => IsVoid(owner.ReturnType) ? 0 : 1,
                CilCode.Call or CilCode.Callvirt => Operand switch
                {
                    MethodAnalysisContext method => method.Parameters.Count + (method.IsStatic ? 0 : 1),
                    MultiDimensionalArrayMethod multiArrayMethod => multiArrayMethod.MethodType switch
                    {
                        MultiDimensionalArrayMethodType.Get => multiArrayMethod.Rank,
                        MultiDimensionalArrayMethodType.Set => multiArrayMethod.Rank + 1,
                        MultiDimensionalArrayMethodType.Address => multiArrayMethod.Rank,
                        _ => throw new NotImplementedException($"Unhandled multidimensional array method type for {Code.Code}"),
                    } + 1,
                    _ => throw new NotImplementedException($"Unhandled operand type for {Code.Code}"),
                },
                CilCode.Newobj => Operand switch
                {
                    MethodAnalysisContext method => method.Parameters.Count,
                    MultiDimensionalArrayMethod multiArrayMethod => multiArrayMethod.MethodType switch
                    {
                        MultiDimensionalArrayMethodType.Constructor => multiArrayMethod.Rank,
                        _ => throw new NotImplementedException($"Unhandled multidimensional array method type for {Code.Code}"),
                    },
                    _ => throw new NotImplementedException($"Unhandled operand type for {Code.Code}"),
                },
                _ => throw new NotImplementedException($"Unhandled var pop count for {Code.Code}"),
            },
            CilStackBehaviour.PopAll => -1, // Special case for 'pop all'
            _ => throw new NotImplementedException($"Unhandled pop count for {Code.StackBehaviourPop}"),
        };
    }

    public int GetPushCount(MethodAnalysisContext owner)
    {
        return Code.StackBehaviourPush switch
        {
            CilStackBehaviour.Push0 => 0,
            CilStackBehaviour.Push1 or CilStackBehaviour.PushI or CilStackBehaviour.PushI8 or CilStackBehaviour.PushR4 or CilStackBehaviour.PushR8 or CilStackBehaviour.PushRef => 1,
            CilStackBehaviour.Push1_Push1 => 2,
            CilStackBehaviour.VarPush => Code.Code switch
            {
                CilCode.Call or CilCode.Callvirt => Operand switch
                {
                    MethodAnalysisContext method => IsVoid(method.ReturnType) ? 0 : 1,
                    MultiDimensionalArrayMethod multiArrayMethod => multiArrayMethod.MethodType switch
                    {
                        MultiDimensionalArrayMethodType.Get or MultiDimensionalArrayMethodType.Address => 1,
                        MultiDimensionalArrayMethodType.Set or MultiDimensionalArrayMethodType.Constructor => 0,
                        _ => throw new NotImplementedException($"Unhandled multidimensional array method type for {Code.Code}"),
                    },
                    _ => throw new NotImplementedException($"Unhandled operand type for {Code.Code}"),
                },
                _ => throw new NotImplementedException($"Unhandled var push count for {Code.Code}"),
            },
            _ => throw new NotImplementedException($"Unhandled push count for {Code.StackBehaviourPush}"),
        };
    }

    private static bool IsVoid(TypeAnalysisContext type)
    {
        if (type.Name is not "Void")
        {
            return false;
        }

        return type == type.AppContext.SystemTypes.SystemVoidType || type == type.AppContext.Il2CppMscorlib.GetTypeByFullName("Il2CppSystem.Void");
    }
}
