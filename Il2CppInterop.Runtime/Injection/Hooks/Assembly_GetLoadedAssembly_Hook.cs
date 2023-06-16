using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Extensions;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Startup;
using Il2CppSystem.Reflection;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class Assembly_GetLoadedAssembly_Hook : Hook<Assembly_GetLoadedAssembly_Hook.MethodDelegate>
    {
        public override MethodDelegate GetDetour() => Hook;
        public override string TargetMethodName => "Assembly::GetLoadedAssembly";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppAssembly* MethodDelegate(IntPtr name);

        private Il2CppAssembly* Hook(IntPtr name)
        {
            var assemblyName = Marshal.PtrToStringAnsi(name);
            Il2CppAssembly* assembly = Original(name);

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
            var assembly = InjectorHelpers.Il2CppMscorlib.GetTypesSafe().SingleOrDefault((x) => x.Name is "Assembly");
            if (assembly == null)
                throw new Exception($"Unity {Il2CppInteropRuntime.Instance.UnityVersion} is not supported at the moment: System.Reflection.Assembly isn't present in Il2Cppmscorlib.dll for unity version, unable to fetch icall");

            var loadWithPartialNameThunk = InjectorHelpers.GetIl2CppMethodPointer(assembly.GetMethod(nameof(Assembly.load_with_partial_name)));
            Logger.Instance.LogTrace("Il2CppSystem.Reflection.Assembly::thunk_load_with_partial_name: 0x{loadWithPartialNameThunk}", loadWithPartialNameThunk.ToInt64().ToString("X2"));

            var loadWithPartialName = XrefScannerLowLevel.JumpTargets(loadWithPartialNameThunk).Last();
            Logger.Instance.LogTrace("Il2CppSystem.Reflection.Assembly::load_with_partial_name: 0x{loadWithPartialName}", loadWithPartialName.ToInt64().ToString("X2"));

            return XrefScannerLowLevel.JumpTargets(loadWithPartialName).ElementAt(1);
        }
    }
}
