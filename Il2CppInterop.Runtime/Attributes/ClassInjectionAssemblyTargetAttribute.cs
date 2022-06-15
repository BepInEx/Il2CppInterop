using System;
using System.Collections.Generic;

namespace Il2CppInterop.Runtime.Attributes;

[AttributeUsage(AttributeTargets.Class)]
public class ClassInjectionAssemblyTargetAttribute : Attribute
{
    private readonly string[] assemblies;

    public ClassInjectionAssemblyTargetAttribute(string assembly)
    {
        if (string.IsNullOrWhiteSpace(assembly)) assemblies = new string[0];
        else assemblies = new[] { assembly };
    }

    public ClassInjectionAssemblyTargetAttribute(string[] assemblies)
    {
        if (assemblies is null) this.assemblies = new string[0];
        else this.assemblies = assemblies;
    }

    internal IntPtr[] GetImagePointers()
    {
        var result = new List<IntPtr>();
        foreach (var assembly in assemblies)
        {
            var intPtr = IL2CPP.GetIl2CppImage(assembly);
            if (intPtr != IntPtr.Zero) result.Add(intPtr);
        }

        return result.ToArray();
    }
}
