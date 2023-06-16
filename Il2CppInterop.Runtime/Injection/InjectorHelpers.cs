using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Injection.Hooks;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    internal static unsafe class InjectorHelpers
    {
        private const string InjectedMonoTypesAssemblyName = "InjectedMonoTypes.dll";
        internal static Assembly Il2CppMscorlib = typeof(Il2CppSystem.Type).Assembly;

        internal static Dictionary<string, IntPtr> InjectedImages = new Dictionary<string, IntPtr>();
        internal static INativeImageStruct DefaultInjectedImage;

        internal static ProcessModule Il2CppModule = Process.GetCurrentProcess()
            .Modules.OfType<ProcessModule>()
            .Single((x) => x.ModuleName is "GameAssembly.dll" or "GameAssembly.so" or "UserAssembly.dll");

        internal static IntPtr Il2CppHandle = NativeLibrary.Load("GameAssembly", typeof(InjectorHelpers).Assembly, null);
        internal static IntPtr UnityPlayerHandle = NativeLibrary.Load("UnityPlayer", typeof(InjectorHelpers).Assembly, null);
        internal static AssemblyIATHooker UnityPlayerIATHooker;
        internal static string NewAssemblyListFile;

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

        private static void CreateDefaultInjectedAssembly()
        {
            DefaultInjectedImage ??= CreateInjectedImage(InjectedMonoTypesAssemblyName);
        }

        private static INativeImageStruct CreateInjectedImage(string name)
        {
            Logger.Instance.LogTrace($"Creating injected assembly {name}");
            var assembly = UnityVersionHandler.NewAssembly();
            var image = UnityVersionHandler.NewImage();

            var nameNoExt = name;
            if (nameNoExt.EndsWith(".dll"))
                nameNoExt = name.Replace(".dll", "");

            assembly.Name.Name = Marshal.StringToHGlobalAnsi(nameNoExt);

            image.Assembly = assembly.AssemblyPointer;
            image.Dynamic = 1;
            image.Name = Marshal.StringToHGlobalAnsi(name);
            if (image.HasNameNoExt)
                image.NameNoExt = assembly.Name.Name;

            assembly.Image = image.ImagePointer;
            InjectedImages.Add(name, image.Pointer);
            return image;
        }

        internal static bool TryGetInjectedImage(string name, out IntPtr ptr)
        {
            if (!name.EndsWith(".dll"))
                name += ".dll";
            return InjectedImages.TryGetValue(name, out ptr);
        }

        internal static bool TryGetInjectedImageForAssembly(Assembly assembly, out IntPtr ptr)
        {
            return TryGetInjectedImage(assembly.GetName().Name, out ptr);
        }

        internal static IntPtr GetOrCreateInjectedImage(string name)
        {
            if (TryGetInjectedImage(name, out var ptr))
                return ptr;

            return CreateInjectedImage(name).Pointer;
        }

        private static readonly GenericMethod_GetMethod_Hook GenericMethodGetMethodHook = new();
        private static readonly MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook GetTypeInfoFromTypeDefinitionIndexHook = new();
        private static readonly Class_GetFieldDefaultValue_Hook GetFieldDefaultValueHook = new();
        private static readonly Class_FromIl2CppType_Hook FromIl2CppTypeHook = new();
        private static readonly Class_FromName_Hook FromNameHook = new();

        private static readonly Assembly_Load_Hook assemblyLoadHook = new();
        private static readonly API_il2cpp_domain_get_assemblies_hook api_get_assemblies = new();

        private static readonly Assembly_GetLoadedAssembly_Hook AssemblyGetLoadedAssemblyHook = new();
        private static readonly AppDomain_GetAssemblies_Hook AppDomainGetAssembliesHook = new();

        internal static void Setup()
        {
            CreateDefaultInjectedAssembly();
            GenericMethodGetMethodHook.ApplyHook();
            GetTypeInfoFromTypeDefinitionIndexHook.ApplyHook();
            GetFieldDefaultValueHook.ApplyHook();
            ClassInit ??= FindClassInit();
            FromIl2CppTypeHook.ApplyHook();
            FromNameHook.ApplyHook();

            AssemblyGetLoadedAssemblyHook.ApplyHook();
            AppDomainGetAssembliesHook.ApplyHook();
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WIN32_FILE_ATTRIBUTE_DATA
        {
            public FileAttributes dwFileAttributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
            public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
        }

        [DllImport("Kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern uint GetFinalPathNameByHandle(IntPtr hFile, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder lpszFilePath, uint cchFilePath, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        public static extern IntPtr CreateFile(
            [MarshalAs(UnmanagedType.LPTStr)] string filename,
            [MarshalAs(UnmanagedType.U4)] FileAccess access,
            [MarshalAs(UnmanagedType.U4)] FileShare share,
            IntPtr securityAttributes, // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
            [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
            [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
            IntPtr templateFile);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int d_GetFileAttributesEx(IntPtr lpFileName, int fInfoLevelId, IntPtr lpFileInformation);

        internal static d_GetFileAttributesEx GetFileAttributesEx;

        private static int GetFileAttributesExDetour(IntPtr lpFileName, int fInfoLevelId, IntPtr lpFileInformation)
        {
            var filePath = Marshal.PtrToStringUni(lpFileName);

            if (filePath.Contains("ScriptingAssemblies.json"))
            {
                filePath = filePath.Replace(@"\\?\", "");

                var assemblyList = new AssemblyListFile(filePath);

                foreach (var assemblyName in InjectedImages.Keys)
                {
                    assemblyList.AddAssembly(assemblyName);
                }

                NewAssemblyListFile = assemblyList.GetTmpFile();
                Logger.Instance.LogInformation($"Forcing unity to read assembly list from {NewAssemblyListFile}");
                var newlpFileName = Marshal.StringToHGlobalUni(NewAssemblyListFile);

                var result = GetFileAttributesEx(newlpFileName, fInfoLevelId, lpFileInformation);
                Marshal.FreeHGlobal(newlpFileName);
                return result;
            }

            return GetFileAttributesEx(lpFileName, fInfoLevelId, lpFileInformation);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int d_ReadFile(IntPtr handle, IntPtr bytes, uint numBytesToRead, IntPtr numBytesRead, NativeOverlapped* overlapped);

        internal static d_ReadFile ReadFile;

        private static int ReadFileDetour(IntPtr handle, IntPtr bytes, uint numBytesToRead, IntPtr numBytesRead, NativeOverlapped* overlapped)
        {
            var sb = new StringBuilder(1024);
            var res = GetFinalPathNameByHandle(handle, sb, 1024, 0);

            if (res != 0)
            {
                var filePath = sb.ToString();
                if (filePath.Contains("ScriptingAssemblies.json"))
                {
                    IntPtr newHandle = CreateFile(NewAssemblyListFile, FileAccess.Read, FileShare.Read, IntPtr.Zero, FileMode.Open, FileAttributes.Normal, IntPtr.Zero);
                    UnpatchIATHooks();
                    return ReadFile(newHandle, bytes, numBytesToRead, numBytesRead, overlapped);
                }
            }

            return ReadFile(handle, bytes, numBytesToRead, numBytesRead, overlapped);
        }

        private static void UnpatchIATHooks()
        {
            Logger.Instance.LogInformation("Unpatching UnityPlayer IAT hooks");
            UnityPlayerIATHooker.UnpatchIATHook("KERNEL32.dll", "ReadFile");
            UnityPlayerIATHooker.UnpatchIATHook("KERNEL32.dll", "GetFileAttributesExW");
        }

        // Setup before unity loads assembly list
        internal static void EarlySetup()
        {
            CreateDefaultInjectedAssembly();
            var allFiles = AssemblyInjectorComponent.ModAssemblies;

            foreach (var file in allFiles)
            {
                var extension = Path.GetExtension(file);
                if (extension.Equals(".dll"))
                {
                    var assemblyName = Path.GetFileName(file);
                    CreateInjectedImage(assemblyName);
                }
            }

            UnityPlayerIATHooker = new AssemblyIATHooker(UnityPlayerHandle);
            UnityPlayerIATHooker.CreateIATHook("KERNEL32.dll", "ReadFile", thunk =>
            {
                ReadFile = Marshal.GetDelegateForFunctionPointer<d_ReadFile>(thunk->Function);
                thunk->Function = Marshal.GetFunctionPointerForDelegate<d_ReadFile>(ReadFileDetour);
            });

            UnityPlayerIATHooker.CreateIATHook("KERNEL32.dll", "GetFileAttributesExW", thunk =>
            {
                GetFileAttributesEx = Marshal.GetDelegateForFunctionPointer<d_GetFileAttributesEx>(thunk->Function);
                thunk->Function = Marshal.GetFunctionPointerForDelegate<d_GetFileAttributesEx>(GetFileAttributesExDetour);
            });

            assemblyLoadHook.ApplyHook();
            api_get_assemblies.ApplyHook();
        }

        internal static long CreateClassToken(IntPtr classPointer)
        {
            long newToken = Interlocked.Decrement(ref s_LastInjectedToken);
            s_InjectedClasses[newToken] = classPointer;
            return newToken;
        }

        internal static void AddTypeToLookup<T>(IntPtr typePointer) where T : class => AddTypeToLookup(typeof(T), typePointer);

        internal static void AddTypeToLookup(Type type, IntPtr typePointer)
        {
            string klass = type.Name;
            if (klass == null) return;
            string namespaze = type.Namespace ?? string.Empty;

            var attribute =
                Attribute.GetCustomAttribute(type,
                    typeof(Attributes.ClassInjectionAssemblyTargetAttribute)) as Attributes.ClassInjectionAssemblyTargetAttribute;

            foreach (IntPtr image in (attribute is null) ? IL2CPP.GetIl2CppImages() : attribute.GetImagePointers())
            {
                s_ClassNameLookup.Add((namespaze, klass, image), typePointer);
            }
        }

        internal static IntPtr GetIl2CppExport(string name)
        {
            if (!TryGetIl2CppExport(name, out var address))
            {
                throw new NotSupportedException($"Couldn't find {name} in {Il2CppModule.ModuleName}'s exports");
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

            FieldInfo methodInfoPointerField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(proxyMethod);
            if (methodInfoPointerField == null)
                throw new ArgumentException($"Couldn't find the generated method info pointer for {proxyMethod.Name}");

            // Il2CppClassPointerStore calls the static constructor for the type
            Il2CppClassPointerStore.GetNativeClassPointer(proxyMethod.DeclaringType);

            IntPtr methodInfoPointer = (IntPtr)methodInfoPointerField.GetValue(null);
            if (methodInfoPointer == IntPtr.Zero)
                throw new ArgumentException($"Generated method info pointer for {proxyMethod.Name} doesn't point to any il2cpp method info");
            INativeMethodInfoStruct methodInfo = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfoPointer);
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
            new MemoryUtils.SignatureDefinition { pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x28\x83", mask = "x????xxxxx", xref = true },
            new MemoryUtils.SignatureDefinition { pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x48\x48", mask = "x????xxxxx", xref = true }
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

                Logger.Instance.LogTrace("GameAssembly.dll: 0x{Il2CppModuleAddress}", Il2CppModule.BaseAddress.ToInt64().ToString("X2"));
                throw new NotSupportedException(
                    "Failed to use signature for Class::Init and a substitute cannot be found, please create an issue and report your unity version & game");
            }

            nint pClassInit = s_ClassInitSignatures
                .Select(s => MemoryUtils.FindSignatureInModule(Il2CppModule, s))
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
    }
}
