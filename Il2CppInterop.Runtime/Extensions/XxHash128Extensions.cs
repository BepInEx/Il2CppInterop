using System;
using System.Buffers;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Common;

namespace Il2CppInterop.Runtime.Extensions;

internal static class XxHash128Extensions
{
    public static void Append(this XxHash128 hash, byte value)
    {
        ReadOnlySpan<byte> data = new ReadOnlySpan<byte>(ref value);
        hash.Append(data);
    }

    public static void Append(this XxHash128 hash, bool value)
    {
        hash.Append((byte)(value ? 1 : 0));
    }

    public static void Append(this XxHash128 hash, ReadOnlySpan<char> characters)
    {
        var data = MemoryMarshal.AsBytes(characters);
        hash.Append(data);
    }

    public static void AppendUtf8(this XxHash128 hash, ReadOnlySpan<char> characters)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(Encoding.UTF8.GetMaxByteCount(characters.Length));
        var bytesWritten = Encoding.UTF8.GetBytes(characters, buffer);
        hash.Append(buffer.AsSpan(0, bytesWritten));
        ArrayPool<byte>.Shared.Return(buffer);
    }

    public static void Append(this XxHash128 hash, Il2CppSystem.String? str)
    {
        hash.Append(GetSpan(str));
    }

    private static unsafe ReadOnlySpan<char> GetSpan(Il2CppSystem.String? str)
    {
        if (str is null)
            return default;

        var pointer = str.Pointer;
        var length = IL2CPP.il2cpp_string_length(pointer);
        var characters = IL2CPP.il2cpp_string_chars(pointer);
        return new ReadOnlySpan<char>(characters, length);
    }
}
