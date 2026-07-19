using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator.Conversions;

internal sealed class MethodCallConversion(MethodAnalysisContext method) : Conversion
{
    public override void Add(List<Instruction> instructions)
    {
        instructions.Add(CilOpCodes.Call, method);
    }
}
