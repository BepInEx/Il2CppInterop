using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.Injection.Hooks;

internal class GarbageCollector_RunFinalizer_Patch : Hook<GarbageCollector_RunFinalizer_Patch.MethodDelegate>
{
    public override string TargetMethodName => "GarbageCollector::RunFinalizer";

    public override MethodDelegate GetDetour() => Hook;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MethodDelegate(IntPtr obj, IntPtr data);

    private void Hook(IntPtr obj, IntPtr data)
    {
        unsafe
        {
            var nativeClassStruct = UnityVersionHandler.Wrap((Il2CppClass*)IL2CPP.il2cpp_object_get_class(obj));
            if (nativeClassStruct.HasFinalize)
            {
                Original(obj, data);
            }
        }
        Il2CppObjectPool.Remove(obj);
    }

    private static readonly MemoryUtils.SignatureDefinition[] s_signatures =
    {
        new()
        {
            // Among Us - 2020.3.22 (x86)
            pattern = "\x55\x8B\xEC\x51\x56\x8B\x75\x08\xC7\x45\x00\x00\x00\x00\x00",
            mask = "xxxxxxxxxx?????",
            xref = false
        },
        new()
        {
            // Test Game - 2021.3.22 (x64)
            pattern = "\x40\x53\x48\x83\xEC\x20\x48\x8B\xD9\x48\xC7\x44\x24\x30\x00\x00\x00\x00\x48\x8B",
            mask = "xxxxxxxxxxxxxxxxxxxx",
            xref = false,
        }
    };

    public override IntPtr FindTargetMethod()
    {
        var methodPtr =  s_signatures
            .Select(s => MemoryUtils.FindSignatureInModule(InjectorHelpers.Il2CppModule, s))
            .FirstOrDefault(p => p != 0);

        if (methodPtr == IntPtr.Zero)
        {
            Il2CppObjectPool.DisableCaching = true;
        }
        return methodPtr;
    }
}
