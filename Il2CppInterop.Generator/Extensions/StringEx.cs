using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using AsmResolver;

namespace Il2CppInterop.Generator.Extensions;

public static class StringEx
{
    public static string MakeValidInSource(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return "";

        char[]? chars = null;
        for (var i = 0; i < str.Length; i++)
        {
            var it = str[i];
            if (IsValidInSource(it))
                continue;

            chars ??= str.ToCharArray();
            chars[i] = '_';
        }

        var result = chars is null ? str : new string(chars);
        return char.IsDigit(result[0]) ? "_" + result : result;
    }

    private static bool IsValidInSource(char c) => char.IsDigit(c) || (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z') || c == '_' || c == '`';

    public static Utf8String MakeValidInSource(this Utf8String? str)
    {
        if (Utf8String.IsNullOrEmpty(str))
            return Utf8String.Empty;

        ReadOnlySpan<byte> data = str.GetBytesUnsafe();

        var length = data.Length;
        byte[]? rentedArray = null;
        Span<byte> rentedArraySpan = default;

        if (char.IsDigit((char)data[0]))
        {
            length++;
            rentedArray = ArrayPool<byte>.Shared.Rent(length);
            rentedArray[0] = (byte)'_';
            rentedArraySpan = rentedArray.AsSpan(1);
            data.CopyTo(rentedArraySpan);
        }

        for (var i = 0; i < data.Length; i++)
        {
            if (IsValidInSource((char)data[i]))
                continue;

            if (rentedArray is null)
            {
                rentedArray = ArrayPool<byte>.Shared.Rent(length);
                rentedArraySpan = rentedArray.AsSpan();
                data.CopyTo(rentedArraySpan);
            }
            rentedArraySpan[i] = (byte)'_';
        }

        if (rentedArray is not null)
        {
            var result = new Utf8String(rentedArray, 0, length);
            ArrayPool<byte>.Shared.Return(rentedArray);
            return result;
        }
        else
        {
            return str;
        }
    }

    public static bool IsInvalidInSource([NotNullWhen(true)] this string? str)
    {
        if (str is null)
            return false;

        for (var i = 0; i < str.Length; i++)
        {
            var it = str[i];
            if (!char.IsDigit(it) && !((it >= 'a' && it <= 'z') || (it >= 'A' && it <= 'Z')) && it != '_' &&
                it != '`') return true;
        }

        return false;
    }

    public static bool IsInvalidInSource([NotNullWhen(true)] this Utf8String? str)
    {
        return IsInvalidInSource(str?.Value);
    }
}
