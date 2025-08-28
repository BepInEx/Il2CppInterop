using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Reflection;

namespace Il2CppInterop.Runtime.InteropTypes.CoreLib;

internal static class MethodBaseAccessor
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetParameters")]
    [return: UnsafeAccessorType($"Il2CppInterop.Runtime.InteropTypes.Arrays.{nameof(Il2CppArrayBase<>)}`1[[Il2CppSystem.Reflection.ParameterInfo, Il2Cppmscorlib]]")]
    private static extern object GetParametersInternal(MethodBase method);

    public static IReadOnlyList<ParameterInfo> GetParameters(this MethodBase method)
    {
        return (IReadOnlyList<ParameterInfo>)GetParametersInternal(method);
    }
}
