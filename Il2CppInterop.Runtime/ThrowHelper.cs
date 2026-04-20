using System;
using System.Runtime.CompilerServices;

namespace Il2CppInterop.Runtime;

internal static class ThrowHelper
{
    internal static void ThrowIfNull(nint ptr, [CallerArgumentExpression(nameof(ptr))] string? paramName = null)
    {
        if (ptr == 0)
        {
            throw new InvalidOperationException($"{paramName} is null");
        }
    }
}
