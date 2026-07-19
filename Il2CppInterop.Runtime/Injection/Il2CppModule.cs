using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.Injection;

internal static class Il2CppModule
{
    internal static readonly ProcessModule Module = Process.GetCurrentProcess()
        .Modules.OfType<ProcessModule>()
        .Single((x) => x.ModuleName is "GameAssembly.dll" or "GameAssembly.dylib" or "GameAssembly.so" or "UserAssembly.dll" || string.Equals(x.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase));

    private static readonly IntPtr Handle = NativeLibrary.Load("GameAssembly", typeof(Il2CppModule).Assembly, null);

    internal static IntPtr GetExport(string name)
    {
        if (!TryGetExport(name, out var address))
        {
            throw new NotSupportedException($"Couldn't find {name} in {Module.ModuleName}'s exports");
        }

        return address;
    }

    internal static bool TryGetExport(string name, out IntPtr address)
    {
        return NativeLibrary.TryGetExport(Handle, name, out address);
    }
}
