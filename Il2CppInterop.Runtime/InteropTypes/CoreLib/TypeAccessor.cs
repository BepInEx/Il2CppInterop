using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Reflection;

namespace Il2CppInterop.Runtime.InteropTypes.CoreLib;

internal static class TypeAccessor
{
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetConstructors")]
    [return: UnsafeAccessorType($"Il2CppInterop.Runtime.InteropTypes.Arrays.{nameof(Il2CppArrayBase<>)}`1[[Il2CppSystem.Reflection.ConstructorInfo, Il2Cppmscorlib]]")]
    private static extern object GetConstructorsInternal(Type type, BindingFlags bindingAttr);

    public static IReadOnlyList<ConstructorInfo> GetConstructors(this Type type, BindingFlags bindingAttr)
    {
        return (IReadOnlyList<ConstructorInfo>)GetConstructorsInternal(type, bindingAttr);
    }
}
