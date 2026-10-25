using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

namespace Il2CppInterop.Generator;

internal static partial class GenericTypeName
{
    public static bool TryMatch(string genericName, [NotNullWhen(true)] out string? typeName, [NotNullWhen(true)] out string? genericCount)
    {
        var match = GenericTypeRegex.Match(genericName);
        if (match.Success)
        {
            typeName = match.Groups[1].Value;
            genericCount = match.Groups[2].Value;
            return true;
        }
        else
        {
            typeName = null;
            genericCount = null;
            return false;
        }
    }

    [GeneratedRegex(@"^(.+)`(\d+)$")]
    private static partial Regex GenericTypeRegex { get; }
}
