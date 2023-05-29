using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Extensions;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;
using Assembly = Il2CppSystem.Reflection.Assembly;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    internal unsafe class AppDomain_GetAssemblies_Hook : Hook<AppDomain_GetAssemblies_Hook.MethodDelegate>
    {
        public override MethodDelegate GetDetour() => Hook;
        public override string TargetMethodName => "AppDomain::GetAssemblies";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr MethodDelegate(IntPtr thisPtr, byte refOnly);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate IntPtr GetAssemblyObject(IntPtr thisPtr);

        private GetAssemblyObject getAssemblyObjectDelegate;

        private IntPtr Hook(IntPtr thisPtr, byte refOnly)
        {
            IntPtr assemblyArrayPtr = Original(thisPtr, refOnly);
            Il2CppReferenceArray<Assembly> assemblyArray = new Il2CppReferenceArray<Assembly>(assemblyArrayPtr);

            if (InjectorHelpers.InjectedImages.Count > 0)
            {
                var newSize = assemblyArray.Length + InjectorHelpers.InjectedImages.Count;
                Il2CppReferenceArray<Assembly> newAssemblyArray = new Il2CppReferenceArray<Assembly>(newSize);
                int i;

                for (i = 0; i < assemblyArray.Length; i++)
                    newAssemblyArray[i] = assemblyArray[i];

                i = assemblyArray.Length;
                foreach (IntPtr imagePtr in InjectorHelpers.InjectedImages.Values)
                {
                    var image = UnityVersionHandler.Wrap((Il2CppImage*)imagePtr);
                    newAssemblyArray[i] = new Assembly(getAssemblyObjectDelegate((IntPtr)image.Assembly));
                    i++;
                }

                return newAssemblyArray.Pointer;
            }

            return assemblyArrayPtr;
        }

        public override IntPtr FindTargetMethod()
        {
            var appDomain = InjectorHelpers.Il2CppMscorlib.GetTypesSafe().SingleOrDefault((x) => x.Name is "AppDomain");
            if (appDomain == null)
                throw new Exception($"Unity {Il2CppInteropRuntime.Instance.UnityVersion} is not supported at the moment: System.AppDomain isn't present in Il2Cppmscorlib.dll for unity version, unable to fetch icall");

            var GetAssembliesThunk = InjectorHelpers.GetIl2CppMethodPointer(appDomain.GetMethod("GetAssemblies", unchecked((BindingFlags)0xffffffff), new[]{typeof(bool)}));
            Logger.Instance.LogTrace("Il2CppSystem.AppDomain::thunk_GetAssemblies: 0x{GetAssembliesThunk}", GetAssembliesThunk.ToInt64().ToString("X2"));

            var myMethod = XrefScannerLowLevel.JumpTargets(GetAssembliesThunk).Single();
            var getAssemblyObject = XrefScannerLowLevel.JumpTargets(myMethod).SkipLast(1).Last();
            Logger.Instance.LogTrace("Reflection::GetAssemblyObject: 0x{getAssemblyObject}", getAssemblyObject.ToInt64().ToString("X2"));

            getAssemblyObjectDelegate = Marshal.GetDelegateForFunctionPointer<GetAssemblyObject>(getAssemblyObject);

            return myMethod;
        }
    }
}
