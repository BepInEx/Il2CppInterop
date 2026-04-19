using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    /// <summary>
    /// Compatibility layer for HybridCLR-modified IL2CPP runtimes.
    /// Provides APIs for detecting HybridCLR runtime and preparing interpreter methods for detouring.
    /// </summary>
    public static class HybridCLRCompat
    {
        /// <summary>
        /// Standard subdirectory name for hotfix interop assemblies.
        /// Output path should be: {InteropDir}/{HotfixInteropSubdir}
        /// e.g., BepInEx/interop/hotfix/
        /// </summary>
        public const string HotfixInteropSubdir = "hotfix";

        /// <summary>
        /// When true, uses the legacy HybridCLR MethodInfo layout where bool fields
        /// (initInterpCallMethodPointer, isInterpterImpl) are stored as independent bytes
        /// AFTER the three pointer fields.
        ///
        /// When false (default), uses the newer layout where these bools are stored as
        /// bit fields (bits 5-6) in the _bitfield0 byte of the standard MethodInfo struct.
        ///
        /// This is auto-detected when first encountering an interpreter method.
        /// You can also set it manually before any HybridCLR method operations.
        /// </summary>
        public static bool UseLegacyMethodInfoLayout
        {
            get => s_UseLegacyLayout;
            set
            {
                s_UseLegacyLayout = value;
                s_LayoutDetected = true;
            }
        }

        private static bool s_UseLegacyLayout = false;
        private static bool s_LayoutDetected = false;

        /// <summary>
        /// Attempts to detect the layout from a specific method.
        /// Only works reliably with interpreter methods (isInterpterImpl = true).
        /// For non-interpreter methods, detection will be inconclusive.
        /// Modders can manually set UseLegacyMethodInfoLayout before any HybridCLR operations.
        /// </summary>
        public static unsafe void DetectLayoutFromMethod(IntPtr methodInfoPtr)
        {
            if (s_LayoutDetected || methodInfoPtr == IntPtr.Zero)
                return;

            var result = ProbeMethodInfoLayout(methodInfoPtr);
            if (result.HasValue)
            {
                s_UseLegacyLayout = result.Value;
                s_LayoutDetected = true;
                Logger.Instance.LogInformation(
                    "HybridCLR MethodInfo layout detected: {Layout}",
                    s_UseLegacyLayout ? "legacy (bool after pointers)" : "new (bitfield)");
            }
            // If inconclusive, don't log - the method might not be an interpreter method
        }

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

        /// <summary>
        /// Probes a method to determine which layout is in use.
        /// Returns true for legacy layout, false for new layout, null if inconclusive.
        /// </summary>
        private static unsafe bool? ProbeMethodInfoLayout(IntPtr methodInfoPtr)
        {
            if (methodInfoPtr == IntPtr.Zero)
                return null;

            byte* ptr = (byte*)methodInfoPtr;

            // Get bitfield offset using ParametersCount address (NOT MethodSize() - 1, which includes padding)
            // MethodSize() = 0x58 (with padding), but actual bitfield0 is at 0x53
            var wrapper = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfoPtr);
            int bitfieldOffset;
            fixed (byte* pCount = &wrapper.ParametersCount)
            {
                bitfieldOffset = (int)(pCount - ptr) + 1;
            }

            // Extension fields start at MethodSize() (after padding)
            int extensionStart = UnityVersionHandler.MethodSize();

            byte bitfield0 = *(ptr + bitfieldOffset);
            bool newLayoutValue = (bitfield0 & 0x40) != 0;

            // LEGACY layout: isInterpterImpl is a byte at extensionStart + IntPtr.Size * 3 + 1
            byte legacyValue = *(ptr + extensionStart + IntPtr.Size * 3 + 1);
            bool legacyLayoutValue = legacyValue != 0;

            Logger.Instance.LogDebug(
                "ProbeMethodInfoLayout: bitfieldOffset=0x{BitfieldOffset:X}, extensionStart=0x{ExtStart:X}, bitfield0=0x{Bitfield:X2}, legacyByte=0x{Legacy:X2}",
                bitfieldOffset, extensionStart, bitfield0, legacyValue);

            // Detection logic:
            // - In NEW layout: bits 5-6 of bitfield0 are used by HybridCLR
            //   - bit 5 = initInterpCallMethodPointer
            //   - bit 6 = isInterpterImpl
            // - In LEGACY layout: bits 5-6 of bitfield0 are unused (should be 0)
            //   - isInterpterImpl is a separate byte (0 or 1) after the pointers
            //
            // Standard il2cpp does NOT use bits 5-6 of bitfield0.

            bool bit6Set = (bitfield0 & 0x40) != 0;
            bool legacyIsValidBool = legacyValue == 0 || legacyValue == 1;

            // Case 1: bit 6 is set -> must be NEW layout (standard il2cpp doesn't use bit 6)
            if (bit6Set)
            {
                return false; // false = new layout
            }

            // Case 2: bit 6 is NOT set, legacy position has valid bool = 1 -> LEGACY layout
            if (!bit6Set && legacyIsValidBool && legacyValue == 1)
            {
                return true; // true = legacy layout
            }

            // Case 3: bit 6 is NOT set, legacy position is 0 -> inconclusive (method might not be interpreter)
            // Case 4: bit 6 is NOT set, legacy position is garbage -> inconclusive
            return null;
        }

        #region HybridCLR Method Detour Support

        // Cache for prepared methods: methodInfoPtr -> original invoker_method
        private static readonly Dictionary<IntPtr, IntPtr> s_PreparedMethods = new();
        private static readonly object s_PreparedMethodsLock = new();

        /// <summary>
        /// Wraps a MethodInfo pointer with HybridCLR extension support.
        /// Returns null if the pointer is invalid.
        /// </summary>
        public static unsafe IHybridCLRMethodInfoStruct WrapMethodInfo(IntPtr methodInfoPtr)
        {
            if (methodInfoPtr == IntPtr.Zero)
                return null;
            return UnityVersionHandler.WrapHybridCLR((Il2CppMethodInfo*)methodInfoPtr);
        }

        /// <summary>
        /// Checks if a method is implemented by the HybridCLR interpreter.
        /// Also triggers layout auto-detection on first interpreter method found.
        /// </summary>
        public static unsafe bool IsInterpreterMethod(IntPtr methodInfoPtr)
        {
            if (!IsHybridCLRRuntime() || methodInfoPtr == IntPtr.Zero)
                return false;

            try
            {
                // Try to auto-detect layout if not yet detected
                if (!s_LayoutDetected)
                    DetectLayoutFromMethod(methodInfoPtr);

                var methodInfo = WrapMethodInfo(methodInfoPtr);
                return methodInfo?.IsInterpterImpl ?? false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Prepares a HybridCLR interpreter method for detouring by copying its bridge code
        /// to a new memory location.
        ///
        /// <para>
        /// <b>Why this is needed:</b><br/>
        /// HybridCLR interpreter methods share bridge functions across multiple methods.
        /// To hook a specific method independently, we must:
        /// <list type="number">
        ///   <item>Copy the bridge function to a new memory location (for calling original)</item>
        ///   <item>Update methodPointer/virtualMethodPointer to the copied bridge</item>
        ///   <item>Preserve invoker_method for calling original code</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// <b>What this method does:</b><br/>
        /// <list type="bullet">
        ///   <item>Copies bridge code to nearby memory (within +/-2GB for rel32 calls)</item>
        ///   <item>Sets methodPointer/virtualMethodPointer to copied bridge</item>
        ///   <item>Caches original invoker_method (restore with <see cref="RestoreInvokerMethod"/>)</item>
        /// </list>
        /// </para>
        /// </summary>
        /// <param name="methodInfoPtr">Pointer to the Il2CppMethodInfo structure.</param>
        /// <returns>
        /// <c>true</c> if the method was prepared successfully;
        /// <c>false</c> if not a HybridCLR runtime, method is not an interpreter method, or preparation failed.
        /// </returns>
        /// <remarks>
        /// After calling this method, you MUST call <see cref="RestoreInvokerMethod"/> to restore
        /// the original invoker_method. Without this, calling the original method will fail.
        /// </remarks>
        public static unsafe bool PrepareMethodForDetour(IntPtr methodInfoPtr)
        {
            Logger.Instance.LogDebug(
                "PrepareMethodForDetour called: methodPtr=0x{MethodPtr:X}, isHybridCLR={IsHybridCLR}",
                (ulong)methodInfoPtr, IsHybridCLRRuntime());

            if (!IsHybridCLRRuntime() || methodInfoPtr == IntPtr.Zero)
                return false;

            // Check if already prepared
            lock (s_PreparedMethodsLock)
            {
                if (s_PreparedMethods.ContainsKey(methodInfoPtr))
                {
                    Logger.Instance.LogTrace("HybridCLR method already prepared: 0x{MethodInfo:X}", (ulong)methodInfoPtr);
                    return true;
                }
            }

            // Try to auto-detect layout if not yet detected
            if (!s_LayoutDetected)
                DetectLayoutFromMethod(methodInfoPtr);

            var methodInfo = WrapMethodInfo(methodInfoPtr);
            if (methodInfo == null || !methodInfo.IsInterpterImpl)
                return false;

            try
            {
                // Get current method pointer (bridge function)
                IntPtr originalMethodPointer = methodInfo.MethodPointer;

                if (originalMethodPointer == IntPtr.Zero)
                {
                    Logger.Instance.LogWarning("HybridCLR method has null methodPointer");
                    return false;
                }

                // Save original invoker_method - CRITICAL for calling original code later
                IntPtr originalInvokerMethod = methodInfo.InvokerMethod;

                Logger.Instance.LogDebug(
                    "HybridCLR PrepareMethodForDetour: methodInfo=0x{MethodInfo:X}, methodPointer=0x{MethodPointer:X}, invoker=0x{Invoker:X}",
                    (ulong)methodInfoPtr, (ulong)originalMethodPointer, (ulong)originalInvokerMethod);

                // Calculate bridge function length using disassembly
                int methodLength = GetMethodLength(originalMethodPointer);
                if (methodLength <= 0)
                {
                    Logger.Instance.LogWarning("Failed to determine HybridCLR bridge length at 0x{Address:X}", (ulong)originalMethodPointer);
                    return false;
                }

                Logger.Instance.LogDebug("HybridCLR bridge length: {Length} bytes", methodLength);

                // Allocate new executable memory NEAR the original code (within ±2GB for rel32 calls)
                IntPtr newCode = AllocateExecutableMemoryNear(originalMethodPointer, methodLength);
                if (newCode == IntPtr.Zero)
                {
                    Logger.Instance.LogWarning("Failed to allocate executable memory for HybridCLR method");
                    return false;
                }

                // Copy the original bridge code
                Buffer.MemoryCopy((void*)originalMethodPointer, (void*)newCode, methodLength, methodLength);

                // Fix relative call/jmp instructions in the copied code.
                // The bridge contains rel32 calls (e.g. to Interpreter::Execute) that are
                // encoded as offsets from the instruction address. After copying to a new
                // location, these offsets point to wrong addresses and must be adjusted.
                RelocateRelativeBranches(newCode, originalMethodPointer, methodLength);

                // Update standard method pointers to point to the new (copied) bridge code
                // This ensures calling the "original" method goes through the copied bridge
                methodInfo.MethodPointer = newCode;
                methodInfo.VirtualMethodPointer = newCode;

                // Cache the original invoker_method for later restoration
                lock (s_PreparedMethodsLock)
                {
                    s_PreparedMethods[methodInfoPtr] = originalInvokerMethod;
                }

                Logger.Instance.LogInformation(
                    "HybridCLR method prepared for detour: 0x{Original:X} -> 0x{New:X} (length: {Length})",
                    (ulong)originalMethodPointer, (ulong)newCode, methodLength);

                return true;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("Failed to prepare HybridCLR method for detour: {Error}", ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Restores the original invoker_method after detour is applied.
        /// This is CRITICAL - without this, calling the original method will fail.
        /// </summary>
        public static unsafe void RestoreInvokerMethod(IntPtr methodInfoPtr)
        {
            if (methodInfoPtr == IntPtr.Zero)
                return;

            IntPtr originalInvoker;
            lock (s_PreparedMethodsLock)
            {
                if (!s_PreparedMethods.TryGetValue(methodInfoPtr, out originalInvoker))
                    return;
            }

            try
            {
                var methodInfo = WrapMethodInfo(methodInfoPtr);
                if (methodInfo != null)
                {
                    methodInfo.InvokerMethod = originalInvoker;
                    Logger.Instance.LogTrace("HybridCLR invoker_method restored: 0x{Invoker:X}", (ulong)originalInvoker);
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("Failed to restore HybridCLR invoker_method: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Determines the length of a method by disassembling until int3 padding or ret is found.
        /// </summary>
        private static unsafe int GetMethodLength(IntPtr codeStart, int maxLength = 0x10000)
        {
            if (codeStart == IntPtr.Zero)
                return 0;

            try
            {
                var stream = new UnmanagedMemoryStream((byte*)codeStart, maxLength, maxLength, FileAccess.Read);
                var codeReader = new StreamCodeReader(stream);
                var decoder = Decoder.Create(IntPtr.Size * 8, codeReader);
                decoder.IP = (ulong)codeStart;

                int length = 0;
                bool foundRet = false;
                int nopCount = 0;

                while (length < maxLength)
                {
                    decoder.Decode(out var instruction);
                    if (decoder.LastError == DecoderError.NoMoreBytes)
                        break;

                    // int3 padding indicates end of function
                    if (instruction.Mnemonic == Mnemonic.Int3)
                        break;

                    // Track consecutive nops after ret - indicates function end
                    if (foundRet && instruction.Mnemonic == Mnemonic.Nop)
                    {
                        nopCount++;
                        if (nopCount >= 2) // 2+ nops after ret = end of function
                            break;
                    }
                    else
                    {
                        nopCount = 0;
                    }

                    length += instruction.Length;

                    // Track if we've seen a ret instruction
                    if (instruction.FlowControl == FlowControl.Return)
                        foundRet = true;
                }

                return length > 0 ? length : 0;
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("GetMethodLength failed: {Error}", ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Fixes relative call/jmp instructions in copied bridge code.
        /// After memcpy, rel32 offsets still point relative to the original location.
        /// This method recalculates them to point to the correct absolute targets.
        /// </summary>
        private static unsafe void RelocateRelativeBranches(IntPtr newCode, IntPtr originalCode, int length)
        {
            try
            {
                var stream = new UnmanagedMemoryStream((byte*)newCode, length, length, FileAccess.Read);
                var codeReader = new StreamCodeReader(stream);
                var decoder = Decoder.Create(64, codeReader);
                decoder.IP = (ulong)newCode;

                long delta = (long)originalCode - (long)newCode;
                int patchCount = 0;

                while (decoder.IP < (ulong)newCode + (ulong)length)
                {
                    int instrOffset = (int)(decoder.IP - (ulong)newCode);
                    decoder.Decode(out var instr);
                    if (decoder.LastError != DecoderError.None)
                        break;

                    if (instr.FlowControl == FlowControl.Call ||
                        instr.FlowControl == FlowControl.UnconditionalBranch ||
                        instr.FlowControl == FlowControl.ConditionalBranch)
                    {
                        ulong target = instr.NearBranchTarget;
                        if (target == 0) continue;

                        bool isInsideBlock = target >= (ulong)newCode && target < (ulong)newCode + (ulong)length;
                        if (isInsideBlock) continue;

                        ulong originalTarget = (ulong)((long)target + delta);
                        int instrEnd = instrOffset + instr.Length;
                        int newRel32 = (int)((long)originalTarget - ((long)newCode + instrEnd));
                        int rel32Pos = instrOffset + instr.Length - 4;

                        byte* patchAddr = (byte*)newCode + rel32Pos;
                        *(int*)patchAddr = newRel32;
                        patchCount++;

                        Logger.Instance.LogTrace(
                            "RelocateRelativeBranches: patched {Mnemonic} at +0x{Offset:X}: 0x{OldTarget:X} -> 0x{NewTarget:X}",
                            instr.Mnemonic, instrOffset, target, originalTarget);
                    }
                }

                if (patchCount > 0)
                    Logger.Instance.LogInformation(
                        "RelocateRelativeBranches: fixed {Count} relative branch(es) in {Length} bytes",
                        patchCount, length);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("RelocateRelativeBranches failed: {Error}", ex.Message);
            }
        }

        /// <summary>
        /// Allocates executable memory near a target address (within +/-2GB for rel32 addressing).
        /// This is critical for HybridCLR bridge duplication - bridge code contains relative calls
        /// that will break if the copy is too far from the original.
        /// </summary>
        private static IntPtr AllocateExecutableMemoryNear(IntPtr targetAddress, int size)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return AllocateExecutableMemoryNearWindows(targetAddress, size);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // EXPERIMENTAL: Linux/macOS support via mmap - not yet tested in production
                return AllocateExecutableMemoryUnix(size);
            }

            Logger.Instance.LogWarning("Unsupported platform for executable memory allocation");
            return IntPtr.Zero;
        }

        private static IntPtr AllocateExecutableMemoryNearWindows(IntPtr targetAddress, int size)
        {
            // Get system allocation granularity (typically 64KB on Windows)
            int allocGranularity = GetAllocationGranularity();
            uint allocSize = (uint)((size + allocGranularity - 1) & ~(allocGranularity - 1));
            if (allocSize < allocGranularity)
                allocSize = (uint)allocGranularity;

            // If no target address, use simple allocation
            if (targetAddress == IntPtr.Zero)
            {
                return VirtualAlloc(IntPtr.Zero, allocSize, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            }

            // Search for free memory within ±2GB of target address
            // rel32 range is ±2GB, but we use a smaller range for safety
            const long searchRange = 0x7FFF0000; // ~2GB
            long targetAddr = targetAddress.ToInt64();
            long minAddr = Math.Max(targetAddr - searchRange, 0x10000); // Don't go below 64KB
            long maxAddr = targetAddr + searchRange;

            // Try to allocate at addresses below the target first (usually more space available)
            long searchStart = (targetAddr - allocGranularity) & ~(allocGranularity - 1);

            // Search downward
            for (long addr = searchStart; addr >= minAddr; addr -= allocGranularity)
            {
                IntPtr result = VirtualAlloc(new IntPtr(addr), allocSize, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
                if (result != IntPtr.Zero)
                {
                    Logger.Instance.LogDebug(
                        "Allocated executable memory near 0x{Target:X} at 0x{Allocated:X} (delta: {Delta})",
                        (ulong)targetAddress, (ulong)result, result.ToInt64() - targetAddr);
                    return result;
                }
            }

            // Search upward
            searchStart = (targetAddr + allocGranularity) & ~(allocGranularity - 1);
            for (long addr = searchStart; addr <= maxAddr; addr += allocGranularity)
            {
                IntPtr result = VirtualAlloc(new IntPtr(addr), allocSize, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
                if (result != IntPtr.Zero)
                {
                    Logger.Instance.LogDebug(
                        "Allocated executable memory near 0x{Target:X} at 0x{Allocated:X} (delta: {Delta})",
                        (ulong)targetAddress, (ulong)result, result.ToInt64() - targetAddr);
                    return result;
                }
            }

            // Fallback: let system choose address (may be too far for rel32)
            Logger.Instance.LogWarning(
                "Could not allocate memory near 0x{Target:X}, falling back to system allocation",
                (ulong)targetAddress);
            return VirtualAlloc(IntPtr.Zero, allocSize, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
        }

        /// <summary>
        /// EXPERIMENTAL: Allocates executable memory on Linux/macOS using mmap.
        /// This implementation has not been tested in production environments.
        /// </summary>
        private static IntPtr AllocateExecutableMemoryUnix(int size)
        {
            // mmap with PROT_READ | PROT_WRITE | PROT_EXEC and MAP_PRIVATE | MAP_ANONYMOUS
            IntPtr result = mmap(IntPtr.Zero, (UIntPtr)size, PROT_READ | PROT_WRITE | PROT_EXEC, MAP_PRIVATE | MAP_ANONYMOUS, -1, 0);

            if (result == MAP_FAILED)
            {
                Logger.Instance.LogWarning("mmap failed for executable memory allocation");
                return IntPtr.Zero;
            }

            return result;
        }

        private static int GetAllocationGranularity()
        {
            GetSystemInfo(out var sysInfo);
            return (int)sysInfo.dwAllocationGranularity;
        }

        // Windows API
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(out SYSTEM_INFO lpSystemInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        // Unix/POSIX API (Linux/macOS)
        private const int PROT_READ = 0x1;
        private const int PROT_WRITE = 0x2;
        private const int PROT_EXEC = 0x4;
        private const int MAP_PRIVATE = 0x02;
        private const int MAP_ANONYMOUS = 0x20; // Linux value; macOS uses 0x1000 but libc handles this
        private static readonly IntPtr MAP_FAILED = new(-1);

        [DllImport("libc", SetLastError = true)]
        private static extern IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, long offset);

        #endregion
    }
}
