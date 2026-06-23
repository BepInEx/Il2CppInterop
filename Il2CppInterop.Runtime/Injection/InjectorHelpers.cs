using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection.Hooks;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    internal static unsafe class InjectorHelpers
    {
        internal static Assembly Il2CppMscorlib = typeof(Il2CppSystem.Type).Assembly;
        internal static INativeAssemblyStruct InjectedAssembly;
        internal static INativeImageStruct InjectedImage;
        internal static IntPtr GameAssemblyBaseAddress => GetGameAssemblyBaseAddress();
        internal static int GameAssemblyMemorySize => GetGameAssemblyMemorySize();
        internal static IntPtr Il2CppHandle => GetIl2CppHandle();

        internal static readonly Dictionary<Type, OpCode> StIndOpcodes = new()
        {
            [typeof(byte)] = OpCodes.Stind_I1,
            [typeof(sbyte)] = OpCodes.Stind_I1,
            [typeof(bool)] = OpCodes.Stind_I1,
            [typeof(short)] = OpCodes.Stind_I2,
            [typeof(ushort)] = OpCodes.Stind_I2,
            [typeof(int)] = OpCodes.Stind_I4,
            [typeof(uint)] = OpCodes.Stind_I4,
            [typeof(long)] = OpCodes.Stind_I8,
            [typeof(ulong)] = OpCodes.Stind_I8,
            [typeof(float)] = OpCodes.Stind_R4,
            [typeof(double)] = OpCodes.Stind_R8
        };

        private static int CalculateMachOSize(IntPtr baseAddress)
        {
            try
            {
                var ptr = (byte*)baseAddress;
                var header = Marshal.PtrToStructure<MachHeader64>((IntPtr)ptr);

                if (header.magic != 0xfeedfacf)
                    return 0; // Ensure it's a valid 64-bit Mach-O header

                long totalSize = 0;
                var currentCmdPtr = ptr + sizeof(MachHeader64);

                for (var i = 0; i < header.ncmds; i++)
                {
                    var lc = Marshal.PtrToStructure<LoadCommand>((IntPtr)currentCmdPtr);

                    if (lc.cmd == 0x19)
                    {
                        var seg = Marshal.PtrToStructure<SegmentCommand64>((IntPtr)currentCmdPtr);

                        // Accumulate virtual memory sizes from valid segments (e.g., __TEXT, __DATA)
                        // We skip the PAGEZERO segment if it exists (usually native executables only, not dylibs)
                        if (seg.segname != "__PAGEZERO")
                        {
                            totalSize += (long)seg.vmsize;
                        }
                    }

                    currentCmdPtr += (int)lc.cmdsize;
                }

                return (int)totalSize;
            }
            catch
            {
                // Fail-safe protection against corrupted memory reads
                return 0;
            }
        }

        private static void CreateInjectedAssembly()
        {
            InjectedAssembly = UnityVersionHandler.NewAssembly();
            InjectedImage = UnityVersionHandler.NewImage();

            InjectedAssembly.Name.Name = Marshal.StringToCoTaskMemUTF8("InjectedMonoTypes");

            InjectedImage.Assembly = InjectedAssembly.AssemblyPointer;
            InjectedImage.Dynamic = 1;
            InjectedImage.Name = InjectedAssembly.Name.Name;

            if (InjectedImage.HasNameNoExt)
                InjectedImage.NameNoExt = InjectedAssembly.Name.Name;
        }

        private static IntPtr GetGameAssemblyBaseAddress()
        {
            if (OperatingSystem.IsMacOS())
            {
                var errorPtr = IntPtr.Zero;
                var libHandle = dlopen("GameAssembly.dylib", 2);

                if (libHandle == IntPtr.Zero)
                {
                    var gameAssemblyPath = MacGameAssemblyPath();

                    if (!string.IsNullOrEmpty(gameAssemblyPath))
                    {
                        libHandle = dlopen(gameAssemblyPath, 2);
                    }
                }

                if (libHandle == IntPtr.Zero)
                {
                    errorPtr = dlerror();

                    var errorMessage = errorPtr != IntPtr.Zero
                        ? Marshal.PtrToStringAnsi(errorPtr)
                        : "Unknown dlopen failure";

                    throw new DllNotFoundException(
                        $"Failed to load \"GameAssembly.dylib\" with error message: {errorMessage}");
                }

                // Clear any previous error state.
                dlerror();

                var symbolAddress = dlsym(libHandle, nameof(IL2CPP.il2cpp_init));
                errorPtr = dlerror();

                if (errorPtr != IntPtr.Zero)
                {
                    var errorMessage = Marshal.PtrToStringAnsi(errorPtr);

                    throw new EntryPointNotFoundException(
                        $"Failed to find symbol \"{nameof(IL2CPP.il2cpp_init)}\" with error message: {errorMessage}");
                }

                if (dladdr(symbolAddress, out var info) == 0)
                {
                    throw new InvalidOperationException();
                }

                var baseAddress = info.dli_fbase;
                return baseAddress;
            }

            return Process.GetCurrentProcess()
                .Modules.OfType<ProcessModule>()
                .Single(x => x.ModuleName is "GameAssembly.dll" or "GameAssembly.so" or "UserAssembly.dll" || string.Equals(x.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                .BaseAddress;
        }

        private static int GetGameAssemblyMemorySize()
        {
            var memorySize = 0;

            if (OperatingSystem.IsMacOS())
            {
                memorySize = CalculateMachOSize(GameAssemblyBaseAddress);
            }
            else
            {
                memorySize = Process.GetCurrentProcess()
                    .Modules.OfType<ProcessModule>()
                    .Single(x => x.ModuleName is "GameAssembly.dll" or "GameAssembly.so" or "UserAssembly.dll" || string.Equals(x.ModuleName, "GameAssembly.dll", StringComparison.OrdinalIgnoreCase))
                    .ModuleMemorySize;
            }

            return memorySize;
        }

        private static IntPtr GetIl2CppHandle()
        {
            var libraryName = "GameAssembly";

            if (OperatingSystem.IsMacOS())
            {
                var gameAssemblyPath = MacGameAssemblyPath();

                if (!string.IsNullOrEmpty(gameAssemblyPath))
                {
                    libraryName = gameAssemblyPath;
                }
            }

            return NativeLibrary.Load(libraryName, typeof(InjectorHelpers).Assembly, null);
        }

        private static string MacGameAssemblyPath()
        {
            var gameAssemblyPath = string.Empty;

            if (Process.GetCurrentProcess().MainModule?.FileName is { } procPath
                && Directory.GetParent(procPath)?.Parent?.FullName is { } appContentsPath
                && Path.GetFileName(appContentsPath) == "Contents")
            {
                gameAssemblyPath = Path.Combine(appContentsPath, "Frameworks", "GameAssembly.dylib");

                if (File.Exists(gameAssemblyPath))
                {
                    return gameAssemblyPath;
                }
            }

            return gameAssemblyPath;
        }

        private static readonly GenericMethod_GetMethod_Hook GenericMethodGetMethodHook = new();
        private static readonly GenericMethod_GetMethod_Unity6_Hook GenericMethodGetMethodHook_Unity6 = new();
        private static readonly MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook GetTypeInfoFromTypeDefinitionIndexHook = new();
        private static readonly Class_GetFieldDefaultValue_Hook GetFieldDefaultValueHook = new();
        private static readonly Class_FromIl2CppType_Hook FromIl2CppTypeHook = new();
        private static readonly Class_FromName_Hook FromNameHook = new();

        internal static void Setup()
        {
            if (InjectedAssembly == null) CreateInjectedAssembly();

            if (Il2CppInteropRuntime.Instance.UnityVersion.Major >= 6000)
                GenericMethodGetMethodHook_Unity6.ApplyHook();
            else
                GenericMethodGetMethodHook.ApplyHook();

            GetTypeInfoFromTypeDefinitionIndexHook.ApplyHook();
            GetFieldDefaultValueHook.ApplyHook();
            ClassInit ??= FindClassInit();
            FromIl2CppTypeHook.ApplyHook();
            FromNameHook.ApplyHook();
        }

        internal static long CreateClassToken(IntPtr classPointer)
        {
            var newToken = Interlocked.Decrement(ref s_LastInjectedToken);
            s_InjectedClasses[newToken] = classPointer;
            return newToken;
        }

        internal static void AddTypeToLookup<T>(IntPtr typePointer) where T : class => AddTypeToLookup(typeof(T), typePointer);

        internal static void AddTypeToLookup(Type type, IntPtr typePointer)
        {
            var klass = type.Name;
            if (klass == null) return;
            var namespaze = type.Namespace ?? string.Empty;
            var attribute = Attribute.GetCustomAttribute(type, typeof(Attributes.ClassInjectionAssemblyTargetAttribute)) as Attributes.ClassInjectionAssemblyTargetAttribute;

            foreach (var image in (attribute is null) ? IL2CPP.GetIl2CppImages() : attribute.GetImagePointers())
            {
                s_ClassNameLookup.Add((namespaze, klass, image), typePointer);
            }
        }

        internal static IntPtr GetIl2CppExport(string name)
        {
            if (!TryGetIl2CppExport(name, out var address))
            {
                var moduleName = Process.GetCurrentProcess().MainModule?.ModuleName;
                throw new NotSupportedException($"Couldn't find {name} in {moduleName}'s exports");
            }

            return address;
        }

        internal static bool TryGetIl2CppExport(string name, out IntPtr address)
        {
            return NativeLibrary.TryGetExport(Il2CppHandle, name, out address);
        }

        internal static IntPtr GetIl2CppMethodPointer(MethodBase proxyMethod)
        {
            if (proxyMethod == null) return IntPtr.Zero;

            var methodInfoPointerField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(proxyMethod);
            if (methodInfoPointerField == null)
                throw new ArgumentException($"Couldn't find the generated method info pointer for {proxyMethod.Name}");

            // Il2CppClassPointerStore calls the static constructor for the type
            Il2CppClassPointerStore.GetNativeClassPointer(proxyMethod.DeclaringType);

            IntPtr methodInfoPointer = (IntPtr)methodInfoPointerField.GetValue(null);
            if (methodInfoPointer == IntPtr.Zero)
                throw new ArgumentException($"Generated method info pointer for {proxyMethod.Name} doesn't point to any il2cpp method info");

            var methodInfo = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfoPointer);
            return methodInfo.MethodPointer;
        }

        private static long s_LastInjectedToken = -2;
        internal static readonly ConcurrentDictionary<long, IntPtr> s_InjectedClasses = new();

        /// <summary> (namespace, class, image) : class </summary>
        internal static readonly Dictionary<(string _namespace, string _class, IntPtr imagePtr), IntPtr> s_ClassNameLookup = new();

        #region Class::Init

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void d_ClassInit(Il2CppClass* klass);
        internal static d_ClassInit ClassInit;

        private static readonly MemoryUtils.SignatureDefinition[] s_ClassInitSignatures =
        {
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x28\x83",
                mask = "x????xxxxx",
                xref = true
            },
            new MemoryUtils.SignatureDefinition
            {
                pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x48\x48",
                mask = "x????xxxxx",
                xref = true
            }
        };

        private static d_ClassInit FindClassInit()
        {
            static nint GetClassInitSubstitute()
            {
                if (TryGetIl2CppExport("mono_class_instance_size", out nint classInit))
                {
                    Logger.Instance.LogTrace("Picked mono_class_instance_size as a Class::Init substitute");
                    return classInit;
                }

                if (TryGetIl2CppExport("mono_class_setup_vtable", out classInit))
                {
                    Logger.Instance.LogTrace("Picked mono_class_setup_vtable as a Class::Init substitute");
                    return classInit;
                }

                if (TryGetIl2CppExport(nameof(IL2CPP.il2cpp_class_has_references), out classInit))
                {
                    Logger.Instance.LogTrace("Picked il2cpp_class_has_references as a Class::Init substitute");
                    return classInit;
                }

                Logger.Instance.LogTrace("GameAssembly: 0x{GameAssemblyAddress}", GameAssemblyBaseAddress.ToInt64().ToString("X2"));
                throw new NotSupportedException("Failed to use signature for Class::Init and a substitute cannot be found, please create an issue and report your unity version & game");
            }

            var pClassInit = s_ClassInitSignatures
                .Select(s => MemoryUtils.FindSignatureInBlock(GameAssemblyBaseAddress, GameAssemblyMemorySize, s))
                .FirstOrDefault(p => p != 0);

            if (pClassInit == 0)
            {
                Logger.Instance.LogWarning("Class::Init signatures have been exhausted, using a substitute!");
                pClassInit = GetClassInitSubstitute();
            }

            Logger.Instance.LogTrace("Class::Init: 0x{PClassInitAddress}", pClassInit.ToString("X2"));

            return Marshal.GetDelegateForFunctionPointer<d_ClassInit>(pClassInit);
        }

        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct DlInfo
        {
            public IntPtr dli_fname;
            public IntPtr dli_fbase;
            public IntPtr dli_sname;
            public IntPtr dli_saddr;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MachHeader64
        {
            public uint magic;
            public int cputype;
            public int cpusubtype;
            public uint filetype;
            public uint ncmds;
            public uint sizeofcmds;
            public uint flags;
            public uint reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct LoadCommand
        {
            public uint cmd;
            public uint cmdsize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SegmentCommand64
        {
            public uint cmd;
            public uint cmdsize;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
            public string segname;

            public ulong vmaddr;
            public ulong vmsize;
            public ulong fileoff;
            public ulong filesize;
            public int maxprot;
            public int initprot;
            public uint nsects;
            public uint flags;
        }

        [DllImport("libSystem.dylib", EntryPoint = "dlopen", CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi)]
        private static extern IntPtr dlopen(string filename, int flags);

        [DllImport("libSystem.dylib", EntryPoint = "dlerror", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr dlerror();

        [DllImport("libSystem.dylib", EntryPoint = "dlsym", CallingConvention = CallingConvention.Cdecl,
            CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);

        [DllImport("libSystem.dylib", EntryPoint = "dladdr", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dladdr(IntPtr addr, out DlInfo info);
    }
}
