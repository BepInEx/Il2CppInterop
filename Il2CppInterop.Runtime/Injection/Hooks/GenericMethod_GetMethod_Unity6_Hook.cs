using System;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection.Hooks
{
    /// Unity 6 (6000.x.x): the 1-param GetMethod(const Il2CppGenericMethod&amp;) is inlined into
    /// the 3-param GetMethod(const MethodInfo*, const Il2CppGenericInst*, const Il2CppGenericInst*).
    /// We use the hook with 3 param correctly
    internal unsafe class GenericMethod_GetMethod_Unity6_Hook : Hook<GenericMethod_GetMethod_Unity6_Hook.MethodDelegate>
    {
        public override string TargetMethodName => "GenericMethod::GetMethod";
        public override MethodDelegate GetDetour() => Hook;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppMethodInfo* MethodDelegate(Il2CppMethodInfo* methodDefinition, Il2CppGenericInst* classInst, Il2CppGenericInst* methodInst);

        private Il2CppMethodInfo* Hook(Il2CppMethodInfo* methodDefinition, Il2CppGenericInst* classInst, Il2CppGenericInst* methodInst)
        {
            if (methodDefinition == null)
                return Original(methodDefinition, classInst, methodInst);

            if (ClassInjector.InflatedMethodFromContextDictionary.TryGetValue((IntPtr)methodDefinition, out var methods))
            {
                if (methodInst == null)
                    return Original(methodDefinition, classInst, methodInst);

                if (methods.Item2.TryGetValue((IntPtr)methodInst, out var inflatedMethodPointer))
                    return (Il2CppMethodInfo*)inflatedMethodPointer;

                var typeArguments = new Type[methodInst->type_argc];
                for (var i = 0; i < methodInst->type_argc; i++)
                    typeArguments[i] = ClassInjector.SystemTypeFromIl2CppType(methodInst->type_argv[i]);
                var inflatedMethod = methods.Item1.MakeGenericMethod(typeArguments);
                Logger.Instance.LogTrace("Inflated method: {InflatedMethod}", inflatedMethod.Name);
                inflatedMethodPointer = (IntPtr)ClassInjector.ConvertMethodInfo(inflatedMethod,
                    UnityVersionHandler.Wrap(UnityVersionHandler.Wrap(methodDefinition).Class));
                methods.Item2.Add((IntPtr)methodInst, inflatedMethodPointer);

                return (Il2CppMethodInfo*)inflatedMethodPointer;
            }

            return Original(methodDefinition, classInst, methodInst);
        }

        public override IntPtr FindTargetMethod()
        {
            var getVirtualMethodAPI = InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_object_get_virtual_method));

            var getVirtualMethod = XrefScannerLowLevel.JumpTargets(getVirtualMethodAPI).Single();

            var getVirtualMethodXrefs = XrefScannerLowLevel.JumpTargets(getVirtualMethod).ToArray();
            if (getVirtualMethodXrefs.Length == 0)
                return IntPtr.Zero;

            // Last xref from Object::GetVirtualMethod is GetGenericVirtualMethod
            var getGenericVirtualMethod = getVirtualMethodXrefs.Last();

            // GetGenericVirtualMethod has a single tail-JMP to GetMethod(3 params)
            var shimXrefs = XrefScannerLowLevel.JumpTargets(getGenericVirtualMethod).ToArray();
            if (shimXrefs.Length == 0)
                return IntPtr.Zero;

            return shimXrefs.Take(2).Last();
        }
    }
}
