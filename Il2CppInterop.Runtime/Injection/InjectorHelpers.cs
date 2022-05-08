using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Class;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.FieldInfo;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;
using Il2CppInterop.Runtime.XrefScans;

namespace Il2CppInterop.Runtime.Injection
{
    internal static unsafe class InjectorHelpers
    {
        internal static INativeAssemblyStruct InjectedAssembly;
        internal static INativeImageStruct InjectedImage;
        internal static ProcessModule Il2CppModule = Process.GetCurrentProcess()
            .Modules.OfType<ProcessModule>()
            .Single((x) => x.ModuleName == "GameAssembly.dll" || x.ModuleName == "UserAssembly.dll");

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

        internal static void Setup()
        {
            if (InjectedAssembly == null) CreateInjectedAssembly();
            GetTypeInfoFromTypeDefinitionIndex ??= FindGetTypeInfoFromTypeDefinitionIndex();
            ClassGetFieldDefaultValue ??= FindClassGetFieldDefaultValue();
            ClassInit ??= FindClassInit();
            ClassFromIl2CppType ??= FindClassFromIl2CppType();
            ClassFromName ??= FindClassFromName();
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

        internal static IntPtr GetIl2CppExport(string exportName, bool throwIfNotExist = true)
        {
            IntPtr addr = GetProcAddress(Il2CppModule.BaseAddress, exportName);
            if (addr == IntPtr.Zero && throwIfNotExist)
                throw new NotSupportedException($"Couldn't find {exportName} in {Il2CppModule.ModuleName}'s exports");
            return addr;
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
        private static readonly ConcurrentDictionary<long, IntPtr> s_InjectedClasses = new();
        /// <summary> (namespace, class, image) : class </summary>
        private static readonly Dictionary<(string _namespace, string _class, IntPtr imagePtr), IntPtr> s_ClassNameLookup = new();

        #region Class::FromName
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppClass* d_ClassFromName(Il2CppImage* image, IntPtr _namespace, IntPtr name);
        private static Il2CppClass* hkClassFromName(Il2CppImage* image, IntPtr _namespace, IntPtr name)
        {
            while (ClassFromNameOriginal == null) Thread.Sleep(1);
            Il2CppClass* classPtr = ClassFromNameOriginal(image, _namespace, name);

            if (classPtr == null)
            {
                string namespaze = Marshal.PtrToStringAnsi(_namespace);
                string className = Marshal.PtrToStringAnsi(name);
                s_ClassNameLookup.TryGetValue((namespaze, className, (IntPtr)image), out IntPtr injectedClass);
                classPtr = (Il2CppClass*)injectedClass;
            }

            return classPtr;
        }
        internal static d_ClassFromName ClassFromName;
        internal static d_ClassFromName ClassFromNameOriginal;
        private static d_ClassFromName FindClassFromName()
        {
#if !MINI
            var classFromNameAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_class_from_name));
            Logger.Trace($"il2cpp_class_from_name: 0x{classFromNameAPI.ToInt64():X2}");

            var classFromName = XrefScannerLowLevel.JumpTargets(classFromNameAPI).Single();
            Logger.Trace($"Class::FromName: 0x{classFromName.ToInt64():X2}");

            ClassFromNameOriginal = ClassInjector.Detour.Detour(classFromName, new d_ClassFromName(hkClassFromName));
            return Marshal.GetDelegateForFunctionPointer<d_ClassFromName>(classFromName);
#else
            return null;
#endif
        }
        #endregion

        #region MetadataCache::GetTypeInfoFromTypeDefinitionIndex
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppClass* d_GetTypeInfoFromTypeDefinitionIndex(int index);
        private static Il2CppClass* hkGetTypeInfoFromTypeDefinitionIndex(int index)
        {
            if (s_InjectedClasses.TryGetValue(index, out IntPtr classPtr))
                return (Il2CppClass*)classPtr;

            while (GetTypeInfoFromTypeDefinitionIndexOriginal == null) Thread.Sleep(1);
            return GetTypeInfoFromTypeDefinitionIndexOriginal(index);
        }
        internal static d_GetTypeInfoFromTypeDefinitionIndex GetTypeInfoFromTypeDefinitionIndex;
        internal static d_GetTypeInfoFromTypeDefinitionIndex GetTypeInfoFromTypeDefinitionIndexOriginal;
        private static d_GetTypeInfoFromTypeDefinitionIndex FindGetTypeInfoFromTypeDefinitionIndex()
        {
#if !MINI
            IntPtr getTypeInfoFromTypeDefinitionIndex = IntPtr.Zero;

            // il2cpp_image_get_class is added in 2018.3.0f1
            if (UnityVersionHandler.UnityVersion < new Version(2018, 3, 0))
            {
                // (Kasuromi): RuntimeHelpers.InitializeArray calls an il2cpp icall, proxy function does some magic before it invokes it
                // https://github.com/Unity-Technologies/mono/blob/unity-2018.2/mcs/class/corlib/System.Runtime.CompilerServices/RuntimeHelpers.cs#L53-L54
                IntPtr runtimeHelpersInitializeArray = GetIl2CppMethodPointer(
                    typeof(Il2CppSystem.Runtime.CompilerServices.RuntimeHelpers)
                        .GetMethod("InitializeArray", new Type[] { typeof(Il2CppSystem.Array), typeof(IntPtr) })
                );
                Logger.Trace($"Il2CppSystem.Runtime.CompilerServices.RuntimeHelpers::InitializeArray: 0x{runtimeHelpersInitializeArray.ToInt64():X2}");

                var runtimeHelpersInitializeArrayICall = XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArray).Last();
                if (XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArrayICall).Count() == 1)
                {
                    // is a thunk function
                    Logger.Trace($"RuntimeHelpers::thunk_InitializeArray: 0x{runtimeHelpersInitializeArrayICall.ToInt64():X2}");
                    runtimeHelpersInitializeArrayICall = XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArrayICall).Single();
                }

                Logger.Trace($"RuntimeHelpers::InitializeArray: 0x{runtimeHelpersInitializeArrayICall.ToInt64():X2}");

                var typeGetUnderlyingType = XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArrayICall).ElementAt(1);
                Logger.Trace($"Type::GetUnderlyingType: 0x{typeGetUnderlyingType.ToInt64():X2}");

                getTypeInfoFromTypeDefinitionIndex = XrefScannerLowLevel.JumpTargets(typeGetUnderlyingType).First();
            }
            else
            {
                var imageGetClassAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_image_get_class));
                Logger.Trace($"il2cpp_image_get_class: 0x{imageGetClassAPI.ToInt64():X2}");

                var imageGetType = XrefScannerLowLevel.JumpTargets(imageGetClassAPI).Single();
                Logger.Trace($"Image::GetType: 0x{imageGetType.ToInt64():X2}");

                var imageGetTypeXrefs = XrefScannerLowLevel.JumpTargets(imageGetType).ToArray();

                if (imageGetTypeXrefs.Length == 0)
                {
                    // (Kasuromi): Image::GetType appears to be inlined in il2cpp_image_get_class on some occasions,
                    // if the unconditional xrefs are 0 then we are in the correct method (seen on unity 2019.3.15)
                    getTypeInfoFromTypeDefinitionIndex = imageGetType;
                }
                else getTypeInfoFromTypeDefinitionIndex = imageGetTypeXrefs[0];
                if (imageGetTypeXrefs.Count() > 1 && UnityVersionHandler.IsMetadataV29OrHigher)
                {
                    // (Kasuromi): metadata v29 introduces handles and adds extra calls, a check for unity versions might be necessary in the future

                    // Second call after obtaining handle, if there are any more calls in the future - correctly index into it if issues occur
                    var getTypeInfoFromHandle = imageGetTypeXrefs.Last();

                    // Two calls, second one (GetIndexForTypeDefinitionInternal) is inlined
                    getTypeInfoFromTypeDefinitionIndex = XrefScannerLowLevel.JumpTargets(getTypeInfoFromHandle).Single();
                }
            }

            Logger.Trace($"MetadataCache::GetTypeInfoFromTypeDefinitionIndex: 0x{getTypeInfoFromTypeDefinitionIndex.ToInt64():X2}");

            GetTypeInfoFromTypeDefinitionIndexOriginal = ClassInjector.Detour.Detour<d_GetTypeInfoFromTypeDefinitionIndex>(
                getTypeInfoFromTypeDefinitionIndex,
                hkGetTypeInfoFromTypeDefinitionIndex
            );
            return Marshal.GetDelegateForFunctionPointer<d_GetTypeInfoFromTypeDefinitionIndex>(getTypeInfoFromTypeDefinitionIndex);
#else
            return null;
#endif
        }
        #endregion

        #region Class::FromIl2CppType
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate Il2CppClass* d_ClassFromIl2CppType(Il2CppTypeStruct* type);
        private static Il2CppClass* hkClassFromIl2CppType(Il2CppTypeStruct* type)
        {
            var wrappedType = UnityVersionHandler.Wrap(type);
            if ((long)wrappedType.Data < 0 && (wrappedType.Type == Il2CppTypeEnum.IL2CPP_TYPE_CLASS || wrappedType.Type == Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE))
            {
                s_InjectedClasses.TryGetValue((long)wrappedType.Data, out var classPointer);
                return (Il2CppClass*)classPointer;
            }

            while (ClassFromIl2CppTypeOriginal == null) Thread.Sleep(1);
            return ClassFromIl2CppTypeOriginal(type);
        }
        internal static d_ClassFromIl2CppType ClassFromIl2CppType;
        internal static d_ClassFromIl2CppType ClassFromIl2CppTypeOriginal;
        private static d_ClassFromIl2CppType FindClassFromIl2CppType()
        {
#if !MINI
            var classFromTypeAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_class_from_il2cpp_type));
            Logger.Trace($"il2cpp_class_from_il2cpp_type: 0x{classFromTypeAPI.ToInt64():X2}");

            var classFromType = XrefScannerLowLevel.JumpTargets(classFromTypeAPI).Single();
            Logger.Trace($"Class::FromIl2CppType: 0x{classFromType.ToInt64():X2}");

            ClassFromIl2CppTypeOriginal = ClassInjector.Detour.Detour(classFromType, new d_ClassFromIl2CppType(hkClassFromIl2CppType));
            return Marshal.GetDelegateForFunctionPointer<d_ClassFromIl2CppType>(classFromType);
#else
            return null;
#endif
        }
        #endregion

        #region Class::GetFieldDefaultValue
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate byte* d_ClassGetFieldDefaultValue(Il2CppFieldInfo* field, out Il2CppTypeStruct* type);
        private static byte* hkClassGetFieldDefaultValue(Il2CppFieldInfo* field, out Il2CppTypeStruct* type)
        {
#if !MINI
            if (EnumInjector.GetDefaultValueOverride(field, out IntPtr newDefaultPtr))
            {
                INativeFieldInfoStruct wrappedField = UnityVersionHandler.Wrap(field);
                INativeClassStruct wrappedParent = UnityVersionHandler.Wrap(wrappedField.Parent);
                INativeClassStruct wrappedElementClass = UnityVersionHandler.Wrap(wrappedParent.ElementClass);
                type = wrappedElementClass.ByValArg.TypePointer;
                return (byte*)newDefaultPtr;
            }
            while (ClassGetFieldDefaultValueOriginal == null) Thread.Sleep(1);
            return ClassGetFieldDefaultValueOriginal(field, out type);
#else
            type = null;
            return null;
#endif
        }
        internal static d_ClassGetFieldDefaultValue ClassGetFieldDefaultValue;
        internal static d_ClassGetFieldDefaultValue ClassGetFieldDefaultValueOriginal;
        private static d_ClassGetFieldDefaultValue FindClassGetFieldDefaultValue()
        {
#if !MINI
            var getStaticFieldValueAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_field_static_get_value));
            Logger.Trace($"il2cpp_field_static_get_value: 0x{getStaticFieldValueAPI.ToInt64():X2}");

            var getStaticFieldValue = XrefScannerLowLevel.JumpTargets(getStaticFieldValueAPI).Single();
            Logger.Trace($"Field::StaticGetValue: 0x{getStaticFieldValue.ToInt64():X2}");

            var getStaticFieldValueInternal = XrefScannerLowLevel.JumpTargets(getStaticFieldValue).Last();
            Logger.Trace($"Field::StaticGetValueInternal: 0x{getStaticFieldValueInternal.ToInt64():X2}");

            // (Kasuromi): The invocation is Field::GetDefaultFieldValue, but this appears to get inlined in all the il2cpp assemblies I've looked at
            // TODO: Add support for non-inlined method invocation
            var classGetDefaultFieldValue = XrefScannerLowLevel.JumpTargets(getStaticFieldValueInternal).First();
            Logger.Trace($"Class::GetDefaultFieldValue: 0x{classGetDefaultFieldValue.ToInt64():X2}");

            ClassGetFieldDefaultValueOriginal = ClassInjector.Detour.Detour(classGetDefaultFieldValue, new d_ClassGetFieldDefaultValue(hkClassGetFieldDefaultValue));
            return Marshal.GetDelegateForFunctionPointer<d_ClassGetFieldDefaultValue>(classGetDefaultFieldValue);
#else
            return null;
#endif
        }
        #endregion

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
            nint pClassInit = s_ClassInitSignatures
                .Select(s => MemoryUtils.FindSignatureInModule(Il2CppModule, s))
                .FirstOrDefault(p => p != 0);

            if (pClassInit == 0)
            {
                // WARN: There might be a race condition with il2cpp_class_has_references
                Logger.Warning("Class::Init signatures have been exhausted, using il2cpp_class_has_references as a substitute!");
                pClassInit = GetIl2CppExport(nameof(IL2CPP.il2cpp_class_has_references));
                if (pClassInit == 0)
                {
                    Logger.Trace($"GameAssembly.dll: 0x{(long)Il2CppModule.BaseAddress}");
                    throw new NotSupportedException("Failed to use signature for Class::Init and il2cpp_class_has_references cannot be found, please create an issue and report your unity version & game");
                }
            }

            Logger.Trace($"Class::Init: 0x{(long)pClassInit:X2}");

            return Marshal.GetDelegateForFunctionPointer<d_ClassInit>(pClassInit);
        }
        #endregion

        #region Imports
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        #endregion
    }
}
