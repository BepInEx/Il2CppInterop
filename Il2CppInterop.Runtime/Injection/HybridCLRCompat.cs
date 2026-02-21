using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Iced.Intel;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;
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

        // HybridCLR extends MethodInfo with additional fields after the standard structure:
        //   void* interpData;
        //   void* methodPointerCallByInterp;
        //   void* virtualMethodPointerCallByInterp;
        //
        // IMPORTANT: initInterpCallMethodPointer and isInterpterImpl are NOT separate fields!
        // They are bit fields within the standard _bitfield0 byte:
        //   bit 5: initInterpCallMethodPointer
        //   bit 6: isInterpterImpl
        //
        // We access these via pointer offsets to avoid modifying INativeMethodInfoStruct interface.

        private static int HybridCLRFieldsOffset => UnityVersionHandler.MethodSize();

        private static int InterpDataOffset => HybridCLRFieldsOffset;
        private static int MethodPointerCallByInterpOffset => HybridCLRFieldsOffset + IntPtr.Size;
        private static int VirtualMethodPointerCallByInterpOffset => HybridCLRFieldsOffset + IntPtr.Size * 2;

        // isInterpterImpl is bit 6 of _bitfield0, which is at offset (MethodSize - 1)
        // The _bitfield0 byte is the last byte of the standard MethodInfo struct
        private static int Bitfield0Offset => UnityVersionHandler.MethodSize() - 1;
        private const byte IsInterpterImplBit = 0x40; // bit 6

        // Cache for prepared methods: methodInfoPtr -> original invoker_method
        private static readonly Dictionary<IntPtr, IntPtr> s_PreparedMethods = new();
        private static readonly object s_PreparedMethodsLock = new();

        /// <summary>
        /// Checks if a method is implemented by the HybridCLR interpreter.
        /// </summary>
        public static unsafe bool IsInterpreterMethod(IntPtr methodInfoPtr)
        {
            if (!IsHybridCLRRuntime() || methodInfoPtr == IntPtr.Zero)
                return false;

            try
            {
                byte* ptr = (byte*)methodInfoPtr;
                byte bitfield0 = *(ptr + Bitfield0Offset);
                bool isInterpImpl = (bitfield0 & IsInterpterImplBit) != 0;
                return isInterpImpl;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Prepares a HybridCLR interpreter method for detouring by copying its bridge code
        /// to a new memory location, allowing independent hooking.
        ///
        /// The key insight from HybridCLR internals:
        /// - Bridge functions are shared across multiple interpreter methods
        /// - We must copy the bridge function for each method we want to hook
        /// - We must preserve the original invoker_method to call original code
        /// - We clear isInterpterImpl so Harmony/MonoMod can hook normally
        /// </summary>
        /// <returns>True if the method was prepared, false if not needed or failed.</returns>
        public static unsafe bool PrepareMethodForDetour(IntPtr methodInfoPtr)
        {
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

            if (!IsInterpreterMethod(methodInfoPtr))
                return false;

            try
            {
                byte* methodInfo = (byte*)methodInfoPtr;

                // Get current method pointer (bridge function)
                IntPtr* methodPointerField = (IntPtr*)methodInfo; // methodPointer is the first field
                IntPtr originalMethodPointer = *methodPointerField;

                if (originalMethodPointer == IntPtr.Zero)
                {
                    Logger.Instance.LogWarning("HybridCLR method has null methodPointer");
                    return false;
                }

                // Save original invoker_method - CRITICAL for calling original code later
                IntPtr* invokerMethodField = (IntPtr*)(methodInfo + IntPtr.Size * 2); // invoker_method is the third field
                IntPtr originalInvokerMethod = *invokerMethodField;

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

                // Allocate new executable memory and copy the bridge code
                IntPtr newCode = AllocateExecutableMemory(methodLength);
                if (newCode == IntPtr.Zero)
                {
                    Logger.Instance.LogWarning("Failed to allocate executable memory for HybridCLR method");
                    return false;
                }

                // Copy the original bridge code
                Buffer.MemoryCopy((void*)originalMethodPointer, (void*)newCode, methodLength, methodLength);

                // Update method pointers to point to the new (copied) bridge code
                *methodPointerField = newCode;

                IntPtr* virtualMethodPointerField = (IntPtr*)(methodInfo + IntPtr.Size);
                *virtualMethodPointerField = newCode;

                // Update HybridCLR-specific pointers
                IntPtr* methodPointerCallByInterpField = (IntPtr*)(methodInfo + MethodPointerCallByInterpOffset);
                if (*methodPointerCallByInterpField != IntPtr.Zero)
                    *methodPointerCallByInterpField = newCode;

                IntPtr* virtualMethodPointerCallByInterpField = (IntPtr*)(methodInfo + VirtualMethodPointerCallByInterpOffset);
                if (*virtualMethodPointerCallByInterpField != IntPtr.Zero)
                    *virtualMethodPointerCallByInterpField = newCode;

                // Clear isInterpterImpl flag (bit 6 of _bitfield0) so Harmony/MonoMod treats this as a native method
                byte* bitfield0Ptr = methodInfo + Bitfield0Offset;
                *bitfield0Ptr = (byte)(*bitfield0Ptr & ~IsInterpterImplBit);

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
        /// NOTE: This method is kept for backward compatibility but Il2CppDetourMethodPatcher
        /// now handles this directly using HybridCLRMethodInfo struct.
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
                byte* methodInfo = (byte*)methodInfoPtr;
                IntPtr* invokerMethodField = (IntPtr*)(methodInfo + IntPtr.Size * 2);
                *invokerMethodField = originalInvoker;

                Logger.Instance.LogTrace("HybridCLR invoker_method restored: 0x{Invoker:X}", (ulong)originalInvoker);
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
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return AllocateExecutableMemoryWindows(size);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // EXPERIMENTAL: Linux/macOS support via mmap - not yet tested in production
                return AllocateExecutableMemoryUnix(size);
            }

            Logger.Instance.LogWarning("Unsupported platform for executable memory allocation");
            return IntPtr.Zero;
        }

        private static IntPtr AllocateExecutableMemoryWindows(int size)
        {
            // Get system allocation granularity (typically 64KB on Windows)
            int allocGranularity = GetAllocationGranularity();

            // Reserve a larger region first, then commit only what we need
            // This leaves space for hook libraries to place their trampolines nearby
            uint reserveSize = (uint)(size + allocGranularity);

            IntPtr reserved = VirtualAlloc(IntPtr.Zero, reserveSize, MEM_RESERVE, PAGE_NOACCESS);
            if (reserved == IntPtr.Zero)
            {
                Logger.Instance.LogWarning("Failed to reserve memory for HybridCLR method");
                return IntPtr.Zero;
            }

            // Release the reservation so we can re-allocate at the same location
            VirtualFree(reserved, 0, MEM_RELEASE);

            // Now allocate the actual code memory at the reserved location
            IntPtr newCode = VirtualAlloc(reserved, (uint)size, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            if (newCode == IntPtr.Zero)
            {
                // Fallback: let system choose address
                newCode = VirtualAlloc(IntPtr.Zero, (uint)size, MEM_RESERVE | MEM_COMMIT, PAGE_EXECUTE_READWRITE);
            }

            return newCode;
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
    }
}
