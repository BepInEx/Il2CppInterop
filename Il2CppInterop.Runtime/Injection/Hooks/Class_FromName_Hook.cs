using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class Class_FromName_Hook : Hook<Class_FromName_Hook.d_ClassFromName>
    {
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppClass* d_ClassFromName(Il2CppImage* image, IntPtr _namespace, IntPtr name);

        private Il2CppClass* hkClassFromName(Il2CppImage* image, IntPtr _namespace, IntPtr name)
        {
            Il2CppClass* classPtr = original(image, _namespace, name);

            if (classPtr == null)
            {
                string namespaze = Marshal.PtrToStringAnsi(_namespace);
                string className = Marshal.PtrToStringAnsi(name);
                InjectorHelpers.s_ClassNameLookup.TryGetValue((namespaze, className, (IntPtr)image), out IntPtr injectedClass);
                classPtr = (Il2CppClass*)injectedClass;
            }

            return classPtr;
        }


        public override d_ClassFromName GetDetour() => new(hkClassFromName);
        public override string TargetMethodName => "Class::FromName";

        public override IntPtr FindTargetMethod()
        {
            var classFromNameAPI = InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_class_from_name));
            Logger.Instance.LogTrace("il2cpp_class_from_name: 0x{ClassFromNameApiAddress}", classFromNameAPI.ToInt64().ToString("X2"));

            return XrefScannerLowLevel.JumpTargets(classFromNameAPI).Single();
        }
    }
}
