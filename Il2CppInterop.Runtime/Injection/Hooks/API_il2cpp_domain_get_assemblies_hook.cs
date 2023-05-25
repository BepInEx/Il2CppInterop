using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class API_il2cpp_domain_get_assemblies_hook : Hook<API_il2cpp_domain_get_assemblies_hook.MethodDelegate>
    {
        public override MethodDelegate GetDetour() => Hook;
        public override string TargetMethodName => "il2cpp_domain_get_assemblies";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr MethodDelegate(IntPtr domain, long* size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr GetAssemblyObject(IntPtr thisPtr);

        private GetAssemblyObject getAssemblyObjectDelegate;

        private IntPtr currentDataPtr = IntPtr.Zero;

        private IntPtr Hook(IntPtr domain, long* size)
        {
            IntPtr assemblyArrayPtr = Original(domain, size);

            if (InjectorHelpers.InjectedImages.Count > 0)
            {
                Il2CppAssembly** oldArray = (Il2CppAssembly**)assemblyArrayPtr;
                int origSize = (int)*size;

                int newSize = origSize + InjectorHelpers.InjectedImages.Count;
                if (currentDataPtr != IntPtr.Zero)
                    Marshal.FreeHGlobal(currentDataPtr);

                currentDataPtr = Marshal.AllocHGlobal(newSize * sizeof(Il2CppSystem.IntPtr));
                Il2CppAssembly** newArray = (Il2CppAssembly**)currentDataPtr;

                int i;

                for (i = 0; i < origSize; i++)
                    newArray[i] = oldArray[i];

                i = origSize;
                foreach (IntPtr imagePtr in InjectorHelpers.InjectedImages.Values)
                {
                    var image = UnityVersionHandler.Wrap((Il2CppImage*)imagePtr);
                    newArray[i] = image.Assembly;
                    i++;
                }

                *size = newSize;
                return currentDataPtr;
            }

            return assemblyArrayPtr;
        }

        public override IntPtr FindTargetMethod()
        {
            return InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_domain_get_assemblies));
        }
    }
}
