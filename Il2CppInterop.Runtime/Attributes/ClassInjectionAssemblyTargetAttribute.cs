using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime.Injection;

namespace Il2CppInterop.Runtime.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ClassInjectionAssemblyTargetAttribute : Attribute
{
    private readonly AssemblyKind assemblyKind;
    private readonly string[] assemblies;

    public ClassInjectionAssemblyTargetAttribute(string assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly)) assemblies = Array.Empty<string>();
        else assemblies = new[] { assembly };
        assemblyKind = AssemblyKind.IL2CPP;
    }

    public ClassInjectionAssemblyTargetAttribute(string[] assemblies)
    {
        if (assemblies is null) this.assemblies = Array.Empty<string>();
        else this.assemblies = assemblies;
        assemblyKind = AssemblyKind.IL2CPP;
    }

    public ClassInjectionAssemblyTargetAttribute(string assembly, AssemblyKind assemblyKind)
    {
        if (string.IsNullOrWhiteSpace(assembly)) assemblies = Array.Empty<string>();
        else assemblies = new[] { assembly };
        this.assemblyKind = assemblyKind;
    }

    public ClassInjectionAssemblyTargetAttribute(string[] assemblies, AssemblyKind assemblyKind)
    {
        if (assemblies is null) this.assemblies = Array.Empty<string>();
        else this.assemblies = assemblies;
        this.assemblyKind = assemblyKind;
    }

    internal IntPtr[] GetImagePointers()
    {
        var result = new List<IntPtr>();
        foreach (var assembly in assemblies)
        {
            IntPtr intPtr;
            switch (assemblyKind)
            {
                case AssemblyKind.IL2CPP:
                    intPtr = IL2CPP.GetIl2CppImage(assembly);
                    if (intPtr != IntPtr.Zero) result.Add(intPtr);
                    break;
                case AssemblyKind.INJECTED:
                    intPtr = InjectorHelpers.GetOrCreateInjectedImage(assembly);
                    if (intPtr != IntPtr.Zero) result.Add(intPtr);
                    break;
            }
        }

        return result.ToArray();
    }
}

public enum AssemblyKind
{
    IL2CPP,
    INJECTED
}
