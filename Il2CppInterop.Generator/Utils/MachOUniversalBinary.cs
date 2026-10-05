namespace Il2CppInterop.Generator.Utils;

/// <summary>
/// Locates one architecture's image inside a universal (fat) Mach-O binary, such as the
/// GameAssembly.dylib shipped by macOS Unity games built for both x86-64 and arm64.
/// </summary>
internal static class MachOUniversalBinary
{
    internal const int CpuTypeX86_64 = 0x01000007;

    private const uint FatMagic = 0xCAFEBABE;
    private const uint FatMagic64 = 0xCAFEBABF;
    private const int FatHeaderSize = 8;
    private const int FatArchSize = 20;
    private const int FatArch64Size = 32;

    /// <summary>
    /// Finds the slice for <paramref name="cpuType"/> when <paramref name="basePtr"/> points to a universal binary.
    /// Returns false for thin binaries, or when the universal binary has no such slice.
    /// </summary>
    internal static unsafe bool TryGetSlice(nint basePtr, long length, int cpuType, out long offset, out long size)
    {
        offset = 0;
        size = 0;

        var bytes = (byte*)basePtr;
        if (length < FatHeaderSize)
            return false;

        var magic = ReadUInt32BigEndian(bytes);
        if (magic != FatMagic && magic != FatMagic64)
            return false;

        var archSize = magic == FatMagic64 ? FatArch64Size : FatArchSize;
        var archCount = ReadUInt32BigEndian(bytes + 4);
        if (FatHeaderSize + archCount * (long)archSize > length)
            return false;

        for (var i = 0; i < archCount; i++)
        {
            var arch = bytes + FatHeaderSize + i * archSize;
            if ((int)ReadUInt32BigEndian(arch) != cpuType)
                continue;

            // fat_arch: cputype, cpusubtype, offset (32-bit), size (32-bit), align
            // fat_arch_64: cputype, cpusubtype, offset (64-bit), size (64-bit), align, reserved
            if (magic == FatMagic64)
            {
                offset = (long)ReadUInt64BigEndian(arch + 8);
                size = (long)ReadUInt64BigEndian(arch + 16);
            }
            else
            {
                offset = ReadUInt32BigEndian(arch + 8);
                size = ReadUInt32BigEndian(arch + 12);
            }

            return offset >= 0 && size > 0 && offset + size <= length;
        }

        return false;
    }

    private static unsafe uint ReadUInt32BigEndian(byte* p) =>
        (uint)(p[0] << 24 | p[1] << 16 | p[2] << 8 | p[3]);

    private static unsafe ulong ReadUInt64BigEndian(byte* p) =>
        (ulong)ReadUInt32BigEndian(p) << 32 | ReadUInt32BigEndian(p + 4);
}
