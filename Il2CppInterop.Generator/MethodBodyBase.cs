using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AssetRipper.CIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils.AsmResolver;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public abstract class MethodBodyBase
{
    public required IReadOnlyList<Instruction> Instructions { get; init; }
    public IReadOnlyList<LocalVariable> LocalVariables { get; init; } = [];
    public IReadOnlyList<ExceptionHandler> ExceptionHandlers { get; init; } = [];

    public void FillMethodBody(MethodDefinition method)
    {
        if (!method.IsManagedMethodWithBody())
        {
            return;
        }

        var body = new CilMethodBody(method);
        method.CilMethodBody = body;
        var module = method.Module!;
        var instructions = body.Instructions;

        var labels = new Dictionary<ILabel, ICilLabel>(Instructions.Count + 1);
        for (var i = Instructions.Count - 1; i >= 0; i--)
        {
            labels.Add(Instructions[i], new CilInstructionLabel());
        }
        labels.Add(EndLabel.Instance, instructions.EndLabel);

        foreach (var exceptionHandler in ExceptionHandlers)
        {
            var handler = new CilExceptionHandler
            {
                HandlerType = exceptionHandler.HandlerType,
                TryStart = labels.GetValue(exceptionHandler.TryStart),
                TryEnd = labels.GetValue(exceptionHandler.TryEnd),
                HandlerStart = labels.GetValue(exceptionHandler.HandlerStart),
                HandlerEnd = labels.GetValue(exceptionHandler.HandlerEnd) ?? instructions.EndLabel,
                FilterStart = labels.GetValue(exceptionHandler.FilterStart),
                ExceptionType = exceptionHandler.ExceptionType?.ToTypeSignature(module).ToTypeDefOrRef(),
            };
            body.ExceptionHandlers.Add(handler);
        }

        Dictionary<LocalVariable, CilLocalVariable> localVariableMap = new(LocalVariables.Count);
        for (var i = LocalVariables.Count - 1; i >= 0; i--)
        {
            var localVariable = LocalVariables[i];
            var localVariableType = localVariable.Type.ToTypeSignature(module);
            var value = new CilLocalVariable(localVariableType);
            body.LocalVariables.Add(value);
            localVariableMap.Add(localVariable, value);
        }

        for (var i = 0; i < Instructions.Count; i++)
        {
            var instruction = Instructions[i];
            var operand = instruction.Operand switch
            {
                This => method.Parameters.ThisParameter,
                LocalVariable localVariable => localVariableMap[localVariable],
                TypeAnalysisContext type => type.ToTypeSignature(module).ToTypeDefOrRef(),
                MethodAnalysisContext methodOperand => methodOperand.ToMethodDescriptor(module),
                FieldAnalysisContext field => field.ToFieldDescriptor(module),
                ParameterAnalysisContext parameter => method.Parameters[parameter.ParameterIndex],
                MultiDimensionalArrayMethod arrayMethod => arrayMethod.ToMethodDescriptor(module),
                ILabel label => labels[label],
                IReadOnlyList<ILabel> labelArray => labelArray.Select(labels.GetValue).ToArray(),
                _ => instruction.Operand,
            };
            var cilInstruction = new CilInstruction(instruction.Code, operand);
            instructions.Add(cilInstruction);
            ((CilInstructionLabel)labels[instruction]).Instruction = cilInstruction;
        }

        instructions.OptimizeMacros();
    }
}
