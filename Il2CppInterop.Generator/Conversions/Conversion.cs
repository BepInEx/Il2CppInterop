using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator.Conversions;

internal abstract class Conversion
{
    public abstract void Add(List<Instruction> instructions);
}
