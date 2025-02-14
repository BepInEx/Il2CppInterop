using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class Assembly_Load_Hook : Hook<Assembly_Load_Hook.MethodDelegate>
    {
        public override MethodDelegate GetDetour() => Hook;
        public override string TargetMethodName => "Assembly::Load";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppAssembly* MethodDelegate(IntPtr name);

        private Il2CppAssembly* Hook(IntPtr name)
        {
            Il2CppAssembly* assembly = Original(name);
            InjectorHelpers.UnpatchIATHooks();
            var assemblyName = Marshal.PtrToStringAnsi(name);

            Logger.Instance.LogInformation($"Assembly::Load {assemblyName}");
            if (assembly == null)
            {
                if (InjectorHelpers.TryGetInjectedImage(assemblyName, out var ptr))
                {
                    var image = UnityVersionHandler.Wrap((Il2CppImage*)ptr);
                    assembly = image.Assembly;
                }
            }

            return assembly;
        }

        public override IntPtr FindTargetMethod()
        {
            var domainAssemblyOpenPtr = InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_domain_assembly_open));
            Logger.Instance.LogTrace("il2cpp_domain_assembly_open: 0x{domainAssemblyOpenPtr}", domainAssemblyOpenPtr.ToInt64().ToString("X2"));

            return XrefScannerLowLevel.JumpTargets(domainAssemblyOpenPtr).Single();
        }
    }
}

