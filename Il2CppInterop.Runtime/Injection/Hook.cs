using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    internal abstract class Hook<T> where T : Delegate
    {
        private bool _isApplied;
        private bool _isSkipped;
        private T _detour;
        private T _method;
        private T _original;

        public T Original => _original;

        public abstract string TargetMethodName { get; }
        public abstract T GetDetour();
        public abstract IntPtr FindTargetMethod();

        public virtual void TargetMethodNotFound()
        {
            throw new Exception($"Required target method {TargetMethodName} not found");
        }

        /// <summary>
        /// Whether a target that resolved into writable+executable memory should be hooked anyway.
        /// Such a target is not a normal function entry: packers/obfuscators emit their stubs into
        /// RWX regions, and detouring one installs a hook on a trampoline that may rewrite its own
        /// arguments and tail-jump elsewhere, so <c>Original(...)</c> no longer honours the delegate
        /// signature. That corrupts state and eventually faults far away from here. Hooks whose
        /// absence is fatal can opt back in by overriding this.
        /// </summary>
        public virtual bool AllowUnsafeTarget => false;

        public void ApplyHook()
        {
            if (_isApplied || _isSkipped) return;

            var methodPtr = FindTargetMethod();

            if (methodPtr == IntPtr.Zero)
            {
                TargetMethodNotFound();
                return;
            }

            if (!AllowUnsafeTarget && MemoryUtils.IsWritableExecutable(methodPtr))
            {
                // Degrade gracefully rather than installing a detour that is known to be unsound:
                // whatever this hook enables stays unavailable, but the process keeps running.
                Logger.Instance.LogWarning(
                    "{MethodName} resolved to 0x{MethodPtr}, which lies in writable+executable memory - " +
                    "this is a packer/obfuscator stub rather than a real function entry, so the hook is " +
                    "being skipped to avoid corrupting the process.",
                    TargetMethodName, methodPtr.ToInt64().ToString("X2"));
                _isSkipped = true;
                return;
            }

            Logger.Instance.LogTrace("{MethodName} found: 0x{MethodPtr}", TargetMethodName, methodPtr.ToInt64().ToString("X2"));

            _detour = GetDetour();
            Detour.Apply(methodPtr, _detour, out _original);
            _method = Marshal.GetDelegateForFunctionPointer<T>(methodPtr);
            _isApplied = true;
        }
    }
}
