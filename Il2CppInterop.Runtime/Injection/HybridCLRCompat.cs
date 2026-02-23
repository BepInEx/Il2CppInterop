using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
    /// Provides APIs for detecting HybridCLR runtime, accessing hotfix assemblies,
    /// and preparing interpreter methods for detouring.
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

        #region HybridCLR Hotfix Assembly Support

        private static readonly HashSet<string> s_HotfixAssemblies = new();
        private static readonly object s_AssemblyLock = new();

        /// <summary>
        /// Refreshes the assembly cache to detect newly loaded HybridCLR hotfix assemblies.
        /// Call this after hotfix DLLs have been loaded by the game.
        /// </summary>
        /// <returns>List of newly detected hotfix assembly names.</returns>
        public static unsafe string[] RefreshHotfixAssemblies()
        {
            if (!IsHybridCLRRuntime())
                return Array.Empty<string>();

            var newAssemblies = new List<string>();

            try
            {
                var domain = IL2CPP.il2cpp_domain_get();
                if (domain == IntPtr.Zero)
                    return Array.Empty<string>();

                uint assembliesCount = 0;
                var assemblies = IL2CPP.il2cpp_domain_get_assemblies(domain, ref assembliesCount);

                for (var i = 0; i < assembliesCount; i++)
                {
                    var image = IL2CPP.il2cpp_assembly_get_image(assemblies[i]);
                    var imageName = IL2CPP.il2cpp_image_get_name_(image);
                    if (string.IsNullOrEmpty(imageName))
                        continue;

                    // Check if this is a new assembly not in the original cache
                    if (IL2CPP.GetIl2CppImage(imageName) != IntPtr.Zero)
                        continue;

                    // Check if it's a hotfix assembly by examining its methods
                    if (IsHotfixImage(image))
                    {
                        lock (s_AssemblyLock)
                        {
                            if (s_HotfixAssemblies.Add(imageName))
                            {
                                newAssemblies.Add(imageName);
                                Logger.Instance.LogInformation("Detected HybridCLR hotfix assembly: {AssemblyName}", imageName);
                            }
                        }

                        // Register in IL2CPP image cache for future access
                        RegisterHotfixImage(imageName, image);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("Failed to refresh hotfix assemblies: {Error}", ex.Message);
            }

            return newAssemblies.ToArray();
        }

        /// <summary>
        /// Gets all detected hotfix assembly names.
        /// </summary>
        public static string[] GetHotfixAssemblyNames()
        {
            lock (s_AssemblyLock)
            {
                return s_HotfixAssemblies.ToArray();
            }
        }

        /// <summary>
        /// Checks if an assembly is a HybridCLR hotfix assembly.
        /// </summary>
        public static bool IsHotfixAssembly(string assemblyName)
        {
            lock (s_AssemblyLock)
            {
                return s_HotfixAssemblies.Contains(assemblyName);
            }
        }

        /// <summary>
        /// Gets a class from a hotfix assembly by name.
        /// </summary>
        public static IntPtr GetHotfixClass(string assemblyName, string namespaceName, string className)
        {
            if (!IsHybridCLRRuntime())
                return IntPtr.Zero;

            // Ensure hotfix assemblies are detected
            if (!IsHotfixAssembly(assemblyName))
                RefreshHotfixAssemblies();

            return IL2CPP.GetIl2CppClass(assemblyName, namespaceName, className);
        }

        /// <summary>
        /// Creates an instance of a hotfix class.
        /// </summary>
        public static IntPtr CreateHotfixInstance(IntPtr klass)
        {
            if (klass == IntPtr.Zero)
                return IntPtr.Zero;

            return IL2CPP.il2cpp_object_new(klass);
        }

        /// <summary>
        /// Creates an instance of a hotfix class by name.
        /// </summary>
        public static IntPtr CreateHotfixInstance(string assemblyName, string namespaceName, string className)
        {
            var klass = GetHotfixClass(assemblyName, namespaceName, className);
            return CreateHotfixInstance(klass);
        }

        /// <summary>
        /// Gets a method from a hotfix class.
        /// </summary>
        public static IntPtr GetHotfixMethod(IntPtr klass, string methodName, int argCount = -1)
        {
            if (klass == IntPtr.Zero)
                return IntPtr.Zero;

            return IL2CPP.il2cpp_class_get_method_from_name(klass, methodName, argCount);
        }

        /// <summary>
        /// Invokes a method on a hotfix object.
        /// </summary>
        public static unsafe IntPtr InvokeHotfixMethod(IntPtr methodInfo, IntPtr obj, void** args)
        {
            if (methodInfo == IntPtr.Zero)
                return IntPtr.Zero;

            IntPtr exception = IntPtr.Zero;
            var result = IL2CPP.il2cpp_runtime_invoke(methodInfo, obj, args, ref exception);

            if (exception != IntPtr.Zero)
            {
                Logger.Instance.LogError("Exception while invoking hotfix method: 0x{ExceptionPtr:X}", (ulong)exception);
                // Let the caller handle the exception - don't rethrow here
            }

            return result;
        }

        /// <summary>
        /// Checks if an image contains interpreter methods (hotfix assembly).
        /// </summary>
        private static unsafe bool IsHotfixImage(IntPtr image)
        {
            // Disabled for now - need to verify HybridCLR MethodInfo structure layout
            // The isInterpterImpl field offset may vary between HybridCLR versions
            return false;

            /*
            if (image == IntPtr.Zero)
                return false;

            try
            {
                uint classCount = IL2CPP.il2cpp_image_get_class_count(image);
                for (uint i = 0; i < Math.Min(classCount, 10); i++) // Check first 10 classes
                {
                    var klass = IL2CPP.il2cpp_image_get_class(image, i);
                    if (klass == IntPtr.Zero)
                        continue;

                    var iter = IntPtr.Zero;
                    IntPtr method;
                    while ((method = IL2CPP.il2cpp_class_get_methods(klass, ref iter)) != IntPtr.Zero)
                    {
                        if (IsInterpreterMethod(method))
                            return true;
                    }
                }
            }
            catch
            {
                // Ignore errors during detection
            }

            return false;
            */
        }

        /// <summary>
        /// Registers a hotfix image in the IL2CPP image cache.
        /// Uses reflection to access the private cache.
        /// </summary>
        private static void RegisterHotfixImage(string name, IntPtr image)
        {
            try
            {
                var field = typeof(IL2CPP).GetField("ourImagesMap", BindingFlags.NonPublic | BindingFlags.Static);
                if (field?.GetValue(null) is Dictionary<string, IntPtr> imagesMap)
                {
                    lock (imagesMap)
                    {
                        imagesMap[name] = image;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("Failed to register hotfix image: {Error}", ex.Message);
            }
        }

        #endregion

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
        /// to a new memory location and configuring the method to call your detour.
        ///
        /// <para>
        /// <b>Why this is needed:</b><br/>
        /// HybridCLR interpreter methods share bridge functions across multiple methods.
        /// To hook a specific method independently, we must:
        /// <list type="number">
        ///   <item>Copy the bridge function to a new memory location (for calling original)</item>
        ///   <item>Configure the method to call your detour instead</item>
        ///   <item>Preserve invoker_method for calling original code</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// <b>What this method does:</b><br/>
        /// <list type="bullet">
        ///   <item>Copies bridge code to nearby memory (within ±2GB for rel32 calls)</item>
        ///   <item>Sets methodPointer to copied bridge (for calling original)</item>
        ///   <item>Sets methodPointerCallByInterp to detour (interpreter calls your code)</item>
        ///   <item>Caches original invoker_method (restore with <see cref="RestoreInvokerMethod"/>)</item>
        /// </list>
        /// </para>
        ///
        /// <para>
        /// <b>Usage example:</b>
        /// <code>
        /// // 1. Define delegate matching the method signature (with methodInfo parameter)
        /// delegate void MyMethodDelegate(IntPtr instance, IntPtr arg1, Il2CppMethodInfo* methodInfo);
        ///
        /// // 2. Create your detour method
        /// static void MyDetour(IntPtr instance, IntPtr arg1, Il2CppMethodInfo* methodInfo)
        /// {
        ///     // Your hook logic here...
        ///
        ///     // Call original: use the methodPointer from methodInfo (points to copied bridge)
        ///     var original = Marshal.GetDelegateForFunctionPointer&lt;MyMethodDelegate&gt;(methodInfo->methodPointer);
        ///     original(instance, arg1, methodInfo);
        /// }
        ///
        /// // 3. Keep delegate alive (prevent GC collection)
        /// static MyMethodDelegate _detourDelegate = MyDetour;
        ///
        /// // 4. Hook the method
        /// IntPtr methodInfoPtr = ...; // Get from Il2Cpp reflection
        /// IntPtr detourAddress = Marshal.GetFunctionPointerForDelegate(_detourDelegate);
        ///
        /// if (HybridCLRCompat.PrepareMethodForDetour(methodInfoPtr, detourAddress))
        /// {
        ///     HybridCLRCompat.RestoreInvokerMethod(methodInfoPtr); // CRITICAL!
        /// }
        /// </code>
        /// </para>
        /// </summary>
        /// <param name="methodInfoPtr">Pointer to the Il2CppMethodInfo structure.</param>
        /// <param name="detourAddress">
        /// Function pointer to your detour method. Obtain via <c>Marshal.GetFunctionPointerForDelegate()</c>.
        /// The delegate must be kept alive to prevent garbage collection.
        /// </param>
        /// <returns>
        /// <c>true</c> if the method was prepared successfully;
        /// <c>false</c> if not a HybridCLR runtime, method is not an interpreter method, or preparation failed.
        /// </returns>
        /// <remarks>
        /// After calling this method, you MUST call <see cref="RestoreInvokerMethod"/> to restore
        /// the original invoker_method. Without this, calling the original method will fail.
        /// </remarks>
        public static unsafe bool PrepareMethodForDetour(IntPtr methodInfoPtr, IntPtr detourAddress)
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

                // Update standard method pointers to point to the new (copied) bridge code
                // This ensures calling the "original" method goes through the copied bridge
                methodInfo.MethodPointer = newCode;
                methodInfo.VirtualMethodPointer = newCode;

                // Set interpreter pointers to detour - when interpreter calls this method,
                // it will call the detour instead of the bridge
                methodInfo.MethodPointerCallByInterp = detourAddress;
                methodInfo.VirtualMethodPointerCallByInterp = detourAddress;
                // Keep isInterpterImpl = true so interpreter uses methodPointerCallByInterp (detour)

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
        /// Checks if a method has been prepared for detouring.
        /// </summary>
        public static bool IsMethodPrepared(IntPtr methodInfoPtr)
        {
            lock (s_PreparedMethodsLock)
            {
                return s_PreparedMethods.ContainsKey(methodInfoPtr);
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
        /// Allocates executable memory for code duplication.
        /// Reserves extra space for hook libraries (Dobby/MonoMod) to use.
        /// </summary>
        private static IntPtr AllocateExecutableMemory(int size)
        {
            return AllocateExecutableMemoryNear(IntPtr.Zero, size);
        }

        /// <summary>
        /// Allocates executable memory near a target address (within ±2GB for rel32 addressing).
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
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_NOACCESS = 0x01;
        private const uint PAGE_EXECUTE_READWRITE = 0x40;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualFree(IntPtr lpAddress, uint dwSize, uint dwFreeType);

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

        /// <summary>
        /// Updates methodPointerCallByInterp and virtualMethodPointerCallByInterp to point to a new address.
        /// This uses the correct dynamically-calculated offsets based on the detected layout.
        /// </summary>
        public static unsafe void UpdateMethodPointerCallByInterp(IntPtr methodInfoPtr, IntPtr newPointer)
        {
            if (methodInfoPtr == IntPtr.Zero)
                return;

            try
            {
                var methodInfo = WrapMethodInfo(methodInfoPtr);
                if (methodInfo == null)
                    return;

                if (methodInfo.MethodPointerCallByInterp != IntPtr.Zero)
                {
                    Logger.Instance.LogTrace(
                        "Updating methodPointerCallByInterp: 0x{Old:X} -> 0x{New:X}",
                        (ulong)methodInfo.MethodPointerCallByInterp, (ulong)newPointer);
                    methodInfo.MethodPointerCallByInterp = newPointer;
                }

                if (methodInfo.VirtualMethodPointerCallByInterp != IntPtr.Zero)
                {
                    Logger.Instance.LogTrace(
                        "Updating virtualMethodPointerCallByInterp: 0x{Old:X} -> 0x{New:X}",
                        (ulong)methodInfo.VirtualMethodPointerCallByInterp, (ulong)newPointer);
                    methodInfo.VirtualMethodPointerCallByInterp = newPointer;
                }
            }
            catch (Exception ex)
            {
                Logger.Instance.LogWarning("Failed to update methodPointerCallByInterp: {Error}", ex.Message);
            }
        }
    }
}
