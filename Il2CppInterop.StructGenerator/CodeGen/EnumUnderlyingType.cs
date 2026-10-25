namespace Il2CppInterop.StructGenerator.CodeGen;

internal enum EnumUnderlyingType
{
    Byte = 0,
    UShort,
    Int,
    UInt,
    ULong
}
internal static class EnumUnderlyingTypeExtensions
{
    public static string ToCSharpString(this EnumUnderlyingType value) => value switch
    {
        EnumUnderlyingType.Byte => "byte",
        EnumUnderlyingType.UShort => "ushort",
        EnumUnderlyingType.Int => "int",
        EnumUnderlyingType.UInt => "uint",
        EnumUnderlyingType.ULong => "ulong",
        _ => throw new InvalidOperationException($"Unknown EnumUnderlyingType value: {value}")
    };
}
