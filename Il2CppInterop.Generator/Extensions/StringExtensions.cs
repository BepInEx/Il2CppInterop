using System.Buffers;

namespace Il2CppInterop.Generator.Extensions;

public static class StringExtensions
{
    public static string MakeValidCSharpName(this string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var array = ArrayPool<char>.Shared.Rent(name.Length + 1);
        array[0] = '_';
        name.AsSpan().CopyTo(array.AsSpan(1));
        for (var i = name.Length; i > 0; i--)
        {
            if (!char.IsLetterOrDigit(array[i]))
            {
                array[i] = '_';
            }
        }
        var result = char.IsDigit(array[1]) ? new string(array.AsSpan(0, name.Length + 1)) : new string(array.AsSpan(1, name.Length));
        ArrayPool<char>.Shared.Return(array);
        return result;
    }
}
