using System;

namespace Il2CppInterop.Runtime.Injection;

internal static unsafe class EnumDefaultValueCodec
{
    internal const int CompressedInt32Size = 5;

    // Unity changed vm::Enum's I4/U4 readers from fixed-width values to
    // ReadCompressedInt32/ReadCompressedUInt32 in the 2021.2 release line.
    internal static bool UsesCompressedInt32(Version unityVersion) => unityVersion >= new Version(2021, 2, 0);

    internal static void WriteCompressedUInt32(byte* destination, uint value)
    {
        // 0xF0 followed by a native little-endian uint is Unity's valid
        // fixed-size form. Keeping the maximum form makes override storage stable.
        destination[0] = 0xF0;
        destination[1] = (byte)value;
        destination[2] = (byte)(value >> 8);
        destination[3] = (byte)(value >> 16);
        destination[4] = (byte)(value >> 24);
    }

    internal static void WriteCompressedInt32(byte* destination, int value)
    {
        var encoded = unchecked(((uint)value << 1) ^ (uint)(value >> 31));
        WriteCompressedUInt32(destination, encoded);
    }
}
