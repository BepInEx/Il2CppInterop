using Il2CppInterop.Runtime.Injection;

Check(!EnumDefaultValueCodec.UsesCompressedInt32(new Version(2021, 1, 99)));
Check(EnumDefaultValueCodec.UsesCompressedInt32(new Version(2021, 2, 0)));
Check(EnumDefaultValueCodec.UsesCompressedInt32(new Version(2022, 3, 62)));
Check(EnumDefaultValueCodec.UsesCompressedInt32(new Version(6000, 0, 58)));

WriteSigned(int.MinValue, "F0FFFFFFFF");
WriteSigned(-1, "F001000000");
WriteSigned(0, "F000000000");
WriteSigned(1, "F002000000");
WriteSigned(int.MaxValue, "F0FEFFFFFF");

WriteUnsigned(0, "F000000000");
WriteUnsigned(127, "F07F000000");
WriteUnsigned(128, "F080000000");
WriteUnsigned(0x12345678, "F078563412");
WriteUnsigned(uint.MaxValue, "F0FFFFFFFF");

unsafe void WriteSigned(int value, string expected)
{
    byte* bytes = stackalloc byte[EnumDefaultValueCodec.CompressedInt32Size];
    EnumDefaultValueCodec.WriteCompressedInt32(bytes, value);
    Write("i", value.ToString(), bytes, expected);
}

unsafe void WriteUnsigned(uint value, string expected)
{
    byte* bytes = stackalloc byte[EnumDefaultValueCodec.CompressedInt32Size];
    EnumDefaultValueCodec.WriteCompressedUInt32(bytes, value);
    Write("u", value.ToString(), bytes, expected);
}

unsafe void Write(string kind, string value, byte* bytes, string expected)
{
    var actual = Convert.ToHexString(new ReadOnlySpan<byte>(bytes, EnumDefaultValueCodec.CompressedInt32Size));
    Check(actual == expected);
    Console.WriteLine($"{kind}\t{value}\t{actual}");
}

static void Check(bool condition)
{
    if (!condition) throw new Exception("Enum default-value regression check failed");
}
