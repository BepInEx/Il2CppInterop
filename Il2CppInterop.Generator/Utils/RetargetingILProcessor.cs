using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Utils;

internal class RetargetingILProcessor
{
    private readonly MethodDefinition _target;
    private readonly Builder _builder;

    // <original destination, new destination>
    private readonly Dictionary<Instruction, Instruction> _replacementBranches = new();
    // <original destination, original branch instructions>
    private readonly Dictionary<Instruction, List<Instruction>> _originalBranches = new();
    public bool NeedsRetargeting => _originalBranches.Count > 0;
    public IReadOnlyDictionary<Instruction, List<Instruction>> IncompleteBranches => _originalBranches;

    private Instruction _originalInstruction;
    private int _trackedIdx = -1;

    public RetargetingILProcessor(MethodDefinition target)
    {
        _target = target;
        _builder = new(this);
    }

    public Builder Track(Instruction originalInstruction)
    {
        if (_originalInstruction != null)
            throw new InvalidOperationException("track called before builder disposed");
        _originalInstruction = originalInstruction;
        _trackedIdx = _target.Body.Instructions.Count;
        return _builder;
    }

    private void TrackBranch(Instruction instruction)
    {
        var operandType = instruction.OpCode.OperandType;
        if (operandType != OperandType.InlineBrTarget &&
            operandType != OperandType.ShortInlineBrTarget)
            return;

        var dst = (Instruction)instruction.Operand;
        if (operandType == OperandType.ShortInlineBrTarget)
            instruction.OpCode = instruction.OpCode.GetLong();

        if (_replacementBranches.TryGetValue(dst, out var newDst))
            instruction.Operand = newDst;
        else
        {
            if (!_originalBranches.TryGetValue(dst, out var oldBranches))
                _originalBranches.Add(dst, oldBranches = new());
            oldBranches.Add(instruction);
        }
    }

    private void RetargetBranches()
    {
        if (_originalInstruction == null)
            throw new InvalidOperationException("builder disposed without calling track");

        if (_trackedIdx < _target.Body.Instructions.Count)
        {
            var newDst = _target.Body.Instructions[_trackedIdx];
            _replacementBranches.Add(_originalInstruction, newDst);
            if (_originalBranches.TryGetValue(_originalInstruction, out var oldBranches))
            {
                foreach (var oldBranch in oldBranches)
                    oldBranch.Operand = newDst;
                _originalBranches.Remove(_originalInstruction);
            }
        }

        _originalInstruction = null;
        _trackedIdx = -1;
    }

    public class Builder : IDisposable
    {
        private readonly RetargetingILProcessor _processor;
        private readonly ILProcessor _targetBuilder;

        public Builder(RetargetingILProcessor processor)
        {
            _processor = processor;
            _targetBuilder = processor._target.Body.GetILProcessor();
        }

        public void Dispose() => _processor.RetargetBranches();

        public void Append(Instruction instruction)
        {
            _processor.TrackBranch(instruction);
            _targetBuilder.Append(instruction);
        }

        // Add whatever equivalent _targetBuilder.Emit you need below

        public void Emit(OpCode opCode) =>
            Append(_targetBuilder.Create(opCode));
        public void Emit(OpCode opCode, FieldReference field) =>
            Append(_targetBuilder.Create(opCode, field));
        public void Emit(OpCode opCode, MethodReference method) =>
            Append(_targetBuilder.Create(opCode, method));
        public void Emit(OpCode opCode, TypeReference type) =>
            Append(_targetBuilder.Create(opCode, type));
        public void Emit(OpCode opCode, VariableDefinition variable) =>
            Append(_targetBuilder.Create(opCode, variable));
    }
}
