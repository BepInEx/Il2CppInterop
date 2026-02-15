using System;
using Il2CppInterop.Common;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    /// <summary>
    /// Compatibility layer for HybridCLR-modified IL2CPP runtimes.
    /// </summary>
    internal static class HybridCLRCompat
    {
        private static bool? s_IsHybridCLR;

        public static bool IsHybridCLRRuntime()
        {
            if (s_IsHybridCLR.HasValue)
                return s_IsHybridCLR.Value;

            s_IsHybridCLR = DetectHybridCLR();
            if (s_IsHybridCLR.Value)
                Logger.Instance.LogWarning("HybridCLR runtime detected - using compatibility mode");

            return s_IsHybridCLR.Value;
        }

        // All known HybridCLR RuntimeApi internal calls (from RuntimeApi.cpp)
        private static readonly string[] s_HybridCLRIcalls =
        {
            "HybridCLR.RuntimeApi::LoadMetadataForAOTAssembly(System.Byte[],HybridCLR.HomologousImageMode)",
            "HybridCLR.RuntimeApi::GetRuntimeOption(HybridCLR.RuntimeOptionId)",
            "HybridCLR.RuntimeApi::SetRuntimeOption(HybridCLR.RuntimeOptionId,System.Int32)",
            "HybridCLR.RuntimeApi::PreJitClass(System.Type)",
            "HybridCLR.RuntimeApi::PreJitMethod(System.Reflection.MethodInfo)",
        };

        private static bool DetectHybridCLR()
        {
            try
            {
                foreach (var icall in s_HybridCLRIcalls)
                {
                    var icallPtr = IL2CPP.il2cpp_resolve_icall(icall);
                    if (icallPtr != IntPtr.Zero)
                    {
                        Logger.Instance.LogTrace("HybridCLR detected via icall: {Icall}", icall);
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
