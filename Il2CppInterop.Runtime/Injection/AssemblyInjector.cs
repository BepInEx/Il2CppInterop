using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection;

internal static unsafe class AssemblyInjector
{
    private static readonly Dictionary<string, INativeImageStruct> images = [];

    internal static INativeImageStruct GetOrCreateImage(string name)
    {
        lock (images)
        {
            if (images.Count == 0)
            {
                var domain = IL2CPP.il2cpp_domain_get();
                if (domain == nint.Zero)
                {
                    Logger.Instance.LogError("No il2cpp domain found; sad!");
                }
                else
                {
                    uint assembliesCount = 0;
                    var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref assembliesCount);
                    for (var i = 0; i < assembliesCount; i++)
                    {
                        var image = UnityVersionHandler.Wrap((Il2CppImage*)IL2CPP.il2cpp_assembly_get_image(assemblies[i]));
                        var imageName = Marshal.PtrToStringUTF8(image.Name)!;
                        images[imageName] = image;
                    }
                }
            }
            if (!images.TryGetValue(name, out var result))
            {
                var assembly = UnityVersionHandler.NewAssembly();
                assembly.Name.Name = Marshal.StringToCoTaskMemUTF8(name);
                result = UnityVersionHandler.NewImage();
                result.Assembly = assembly.AssemblyPointer;
                result.Dynamic = 1;
                result.Name = assembly.Name.Name;
                if (result.HasNameNoExt)
                {
                    if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        result.NameNoExt = Marshal.StringToCoTaskMemUTF8(name.Substring(0, name.Length - 4));
                    }
                    else
                    {
                        result.NameNoExt = assembly.Name.Name;
                    }
                }
                images[name] = result;
            }
            return result;
        }
    }
}
