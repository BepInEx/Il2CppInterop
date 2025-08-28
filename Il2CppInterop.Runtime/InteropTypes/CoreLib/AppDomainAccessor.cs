using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem.Reflection;

namespace Il2CppInterop.Runtime.InteropTypes.CoreLib;

internal static class AppDomainAccessor
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetAssemblies")]
    [return: UnsafeAccessorType($"Il2CppInterop.Runtime.InteropTypes.Arrays.{nameof(Il2CppArrayBase<>)}`1[[Il2CppSystem.Reflection.Assembly, Il2Cppmscorlib]]")]
    public static extern object GetAssemblies([UnsafeAccessorType("Il2CppSystem.AppDomain, Il2Cppmscorlib")] object appDomain);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_CurrentDomain")]
    [return: UnsafeAccessorType("Il2CppSystem.AppDomain, Il2Cppmscorlib")]
    public static extern object GetCurrentDomain([UnsafeAccessorType("Il2CppSystem.AppDomain, Il2Cppmscorlib")] object? declaringType = null);

    public static IReadOnlyList<Assembly> GetAssembliesInCurrentDomain()
    {
        return (IReadOnlyList<Assembly>)GetAssemblies(GetCurrentDomain());
    }
}
