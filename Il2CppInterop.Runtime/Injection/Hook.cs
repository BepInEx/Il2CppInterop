using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    public abstract class Hook<T> where T : Delegate
    {
        private bool isApplied;
        private T detour;
        internal T method;
        internal T original;


        public abstract string TargetMethodName { get; }
        public abstract T GetDetour();
        public abstract IntPtr FindTargetMethod();

        public void ApplyHook()
        {
            if (isApplied) return;

            IntPtr methodPtr = FindTargetMethod();
            Logger.Instance.LogTrace("{MethodName} found: 0x{MethodPtr}",TargetMethodName, methodPtr.ToInt64().ToString("X2"));

            detour = GetDetour();
            Detour.Apply(methodPtr, detour, out original);
            method = Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
            isApplied = true;
        }

    }
}
