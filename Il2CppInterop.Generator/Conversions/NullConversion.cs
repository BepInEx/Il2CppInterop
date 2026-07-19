using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator.Conversions;

internal sealed class NullConversion : Conversion
{
    public static NullConversion Instance = new();

    private NullConversion()
    {
    }

    public override void Add(List<Instruction> instructions)
    {
    }
}
