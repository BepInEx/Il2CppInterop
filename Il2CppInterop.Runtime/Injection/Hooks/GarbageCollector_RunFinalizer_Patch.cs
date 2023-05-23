using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks;

internal class GarbageCollector_RunFinalizer_Patch : Hook<GarbageCollector_RunFinalizer_Patch.MethodDelegate>
{
    public override string TargetMethodName => "GarbageCollector::RunFinalizer";

    public override MethodDelegate GetDetour() => Hook;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MethodDelegate(IntPtr obj, IntPtr data);

    private void Hook(IntPtr obj, IntPtr data)
    {
        Original(obj, data);
        Il2CppObjectPool.Remove(obj);
    }

    private static readonly MemoryUtils.SignatureDefinition[] s_signatures =
    {
        new()
        {
            pattern = "\x55\x8B\xEC\x51\x56\x8B\x75\x08\xC7\x45\x00\x00\x00\x00\x00",
            mask = "xxxxxxxxxx?????",
            xref = false
        }
    };

    public override IntPtr FindTargetMethod() => s_signatures
        .Select(s => MemoryUtils.FindSignatureInModule(InjectorHelpers.Il2CppModule, s))
        .FirstOrDefault(p => p != 0);
}
