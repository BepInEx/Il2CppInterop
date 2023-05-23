using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Extensions;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Injection.Hooks;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.FieldInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.Startup;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection
{
    internal static unsafe class InjectorHelpers
    {
        internal static Assembly Il2CppMscorlib = typeof(Il2CppSystem.Type).Assembly;
        internal static INativeAssemblyStruct InjectedAssembly;
        internal static INativeImageStruct InjectedImage;
        internal static ProcessModule Il2CppModule = Process.GetCurrentProcess()
            .Modules.OfType<ProcessModule>()
            .Single((x) => x.ModuleName is "GameAssembly.dll" or "GameAssembly.so" or "UserAssembly.dll");

        internal static IntPtr Il2CppHandle = NativeLibrary.Load("GameAssembly", typeof(InjectorHelpers).Assembly, null);

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

        private static void CreateInjectedAssembly()
        {
            InjectedAssembly = UnityVersionHandler.NewAssembly();
            InjectedImage = UnityVersionHandler.NewImage();

            InjectedAssembly.Name.Name = Marshal.StringToHGlobalAnsi("InjectedMonoTypes");

            InjectedImage.Assembly = InjectedAssembly.AssemblyPointer;
            InjectedImage.Dynamic = 1;
            InjectedImage.Name = InjectedAssembly.Name.Name;
            if (InjectedImage.HasNameNoExt)
                InjectedImage.NameNoExt = InjectedAssembly.Name.Name;
        }

        private static readonly GenericMethod_GetMethod_Hook GenericMethodGetMethodHook = new();
        private static readonly MetadataCache_GetTypeInfoFromTypeDefinitionIndex_Hook GetTypeInfoFromTypeDefinitionIndexHook = new();
        private static readonly Class_GetFieldDefaultValue_Hook GetFieldDefaultValueHook = new();
        private static readonly Class_FromIl2CppType_Hook FromIl2CppTypeHook = new();
        private static readonly Class_FromName_Hook FromNameHook = new();
        private static readonly GarbageCollector_RunFinalizer_Patch RunFinalizerPatch = new();

        internal static void Setup()
        {
            if (InjectedAssembly == null) CreateInjectedAssembly();
            GenericMethodGetMethodHook.ApplyHook();
            GetTypeInfoFromTypeDefinitionIndexHook.ApplyHook();
            GetFieldDefaultValueHook.ApplyHook();
            ClassInit ??= FindClassInit();
            FromIl2CppTypeHook.ApplyHook();
            FromNameHook.ApplyHook();
            RunFinalizerPatch.ApplyHook();
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
            var attribute = Attribute.GetCustomAttribute(type, typeof(Il2CppInterop.Runtime.Attributes.ClassInjectionAssemblyTargetAttribute)) as Il2CppInterop.Runtime.Attributes.ClassInjectionAssemblyTargetAttribute;

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

                Logger.Instance.LogTrace("GameAssembly.dll: 0x{Il2CppModuleAddress}", Il2CppModule.BaseAddress.ToInt64().ToString("X2"));
                throw new NotSupportedException("Failed to use signature for Class::Init and a substitute cannot be found, please create an issue and report your unity version & game");
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
