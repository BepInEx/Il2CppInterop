using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSystem;
using Il2CppSystem.Reflection;

namespace Il2CppInterop.Runtime.XrefScans;

internal class XrefScanImpl : IXrefScannerImpl
{
    public unsafe (XrefScanUtil.InitMetadataForMethod, nint)? GetMetadataResolver()
    {
        var unityObjectCctor = GetAssembliesInCurrentDomain()
            .Single(it => it.GetName().Name == "UnityEngine.CoreModule").GetType("UnityEngine.Object")
            .GetConstructors(BindingFlags.Static | BindingFlags.NonPublic)
            .Single();
        var nativeMethodInfo = IL2CPP.il2cpp_method_get_from_reflection(unityObjectCctor.Pointer);
        var ourMetadataInitForMethodPointer = XrefScannerLowLevel.JumpTargets(*(nint*)nativeMethodInfo).First();
        var ourMetadataInitForMethodDelegate =
            Marshal.GetDelegateForFunctionPointer<XrefScanUtil.InitMetadataForMethod>(ourMetadataInitForMethodPointer);
        return (ourMetadataInitForMethodDelegate, ourMetadataInitForMethodPointer);
    }

    public bool XrefGlobalClassFilter(nint movTarget)
    {
        var valueAtMov = (nint)Marshal.ReadInt64(movTarget);
        if (valueAtMov != nint.Zero)
        {
            var targetClass = (nint)Marshal.ReadInt64(valueAtMov);
            return targetClass == Il2CppClassPointerStore<String>.NativeClassPtr ||
                   targetClass == Il2CppClassPointerStore<Type>.NativeClassPtr;
        }

        return false;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "GetAssemblies")]
    [return: UnsafeAccessorType($"Il2CppInterop.Runtime.InteropTypes.Arrays.{nameof(Il2CppArrayBase<>)}`1[[Il2CppSystem.Reflection.Assembly, Il2Cppmscorlib]]")]
    private static extern object GetAssemblies([UnsafeAccessorType("Il2CppSystem.AppDomain, Il2Cppmscorlib")] object appDomain);

    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "get_CurrentDomain")]
    [return: UnsafeAccessorType("Il2CppSystem.AppDomain, Il2Cppmscorlib")]
    private static extern object GetCurrentDomain();

    private static IEnumerable<Assembly> GetAssembliesInCurrentDomain()
    {
        return (IEnumerable<Assembly>)GetAssemblies(GetCurrentDomain());
    }
}
