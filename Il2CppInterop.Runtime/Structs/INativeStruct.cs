namespace Il2CppInterop.Runtime.Structs;

public interface INativeStruct
{
    nint Pointer { get; }
}
internal static class INativeStructExtensions
{
    public static unsafe bool CheckBit(this INativeStruct self, int startOffset, int bit)
    {
        var byteOffset = bit / 8;
        var bitOffset = bit % 8;
        var p = self.Pointer + startOffset + byteOffset;

        var mask = 1 << bitOffset;
        var val = *(byte*)p.ToPointer();
        var masked = val & mask;
        return masked == mask;
    }

    public static unsafe void SetBit(this INativeStruct self, int startOffset, int bit, bool value)
    {
        var byteOffset = bit / 8;
        var bitOffset = bit % 8;
        var p = self.Pointer + startOffset + byteOffset;

        var mask = ~(1 << bitOffset);
        var ptr = (byte*)p.ToPointer();
        var val = *ptr;
        var newVal = (byte)((val & mask) | ((value ? 1 : 0) << bitOffset));
        *ptr = newVal;
    }
}
