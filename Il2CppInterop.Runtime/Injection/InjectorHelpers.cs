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
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Attributes;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Image;
using Il2CppSystem.Runtime.CompilerServices;
using Array = Il2CppSystem.Array;

namespace Il2CppInterop.Runtime.Injection;

internal static unsafe class InjectorHelpers
{
    internal static INativeAssemblyStruct InjectedAssembly;
    internal static INativeImageStruct InjectedImage;

    internal static ProcessModule Il2CppModule = Process.GetCurrentProcess()
        .Modules.OfType<ProcessModule>()
        .Single(x => x.ModuleName == "GameAssembly.dll" || x.ModuleName == "UserAssembly.dll");

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

    private static long s_LastInjectedToken = -2;
    private static readonly ConcurrentDictionary<long, IntPtr> s_InjectedClasses = new();

    /// <summary> (namespace, class, image) : class </summary>
    private static readonly Dictionary<(string _namespace, string _class, IntPtr imagePtr), IntPtr> s_ClassNameLookup =
        new();

    // (Kasuromi): This is required for CoreCLR to prevent delegates from getting garbage collected (GCHandles didn't seem to work)
    private static readonly List<object> _delegateCache = new();

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
        if (GenericMethodGetMethod == null)
        {
            GenericMethodGetMethod = FindGenericMethodGetMethod();
#if NET6_0
            _delegateCache.Add(GenericMethodGetMethod);
#endif
        }

        if (GetTypeInfoFromTypeDefinitionIndex == null)
        {
            GetTypeInfoFromTypeDefinitionIndex = FindGetTypeInfoFromTypeDefinitionIndex();
#if NET6_0
            _delegateCache.Add(GetTypeInfoFromTypeDefinitionIndex);
#endif
        }

        if (ClassGetFieldDefaultValue == null)
        {
            ClassGetFieldDefaultValue = FindClassGetFieldDefaultValue();
#if NET6_0
            _delegateCache.Add(ClassGetFieldDefaultValue);
#endif
        }

        if (ClassInit == null)
        {
            ClassInit = FindClassInit();
#if NET6_0
            _delegateCache.Add(ClassInit);
#endif
        }

        if (ClassFromIl2CppType == null)
        {
            ClassFromIl2CppType = FindClassFromIl2CppType();
#if NET6_0
            _delegateCache.Add(ClassFromIl2CppType);
#endif
        }

        if (ClassFromName == null)
        {
            ClassFromName = FindClassFromName();
#if NET6_0
            _delegateCache.Add(ClassFromName);
#endif
        }
    }

    internal static long CreateClassToken(IntPtr classPointer)
    {
        var newToken = Interlocked.Decrement(ref s_LastInjectedToken);
        s_InjectedClasses[newToken] = classPointer;
        return newToken;
    }

    internal static void AddTypeToLookup<T>(IntPtr typePointer) where T : class
    {
        AddTypeToLookup(typeof(T), typePointer);
    }

    internal static void AddTypeToLookup(Type type, IntPtr typePointer)
    {
        var klass = type.Name;
        if (klass == null) return;
        var namespaze = type.Namespace ?? string.Empty;
        var attribute =
            Attribute.GetCustomAttribute(type, typeof(ClassInjectionAssemblyTargetAttribute)) as
                ClassInjectionAssemblyTargetAttribute;

        foreach (var image in attribute is null ? IL2CPP.GetIl2CppImages() : attribute.GetImagePointers())
            s_ClassNameLookup.Add((namespaze, klass, image), typePointer);
    }

    internal static IntPtr GetIl2CppExport(string exportName, bool throwIfNotExist = true)
    {
        var addr = GetProcAddress(Il2CppModule.BaseAddress, exportName);
        if (addr == IntPtr.Zero && throwIfNotExist)
            throw new NotSupportedException($"Couldn't find {exportName} in {Il2CppModule.ModuleName}'s exports");
        return addr;
    }

    internal static IntPtr GetIl2CppMethodPointer(MethodBase proxyMethod)
    {
        if (proxyMethod == null) return IntPtr.Zero;

        var methodInfoPointerField = Il2CppInteropUtils.GetIl2CppMethodInfoPointerFieldForGeneratedMethod(proxyMethod);
        if (methodInfoPointerField == null)
            throw new ArgumentException($"Couldn't find the generated method info pointer for {proxyMethod.Name}");

        // Il2CppClassPointerStore calls the static constructor for the type
        Il2CppClassPointerStore.GetNativeClassPointer(proxyMethod.DeclaringType);

        var methodInfoPointer = (IntPtr)methodInfoPointerField.GetValue(null);
        if (methodInfoPointer == IntPtr.Zero)
            throw new ArgumentException(
                $"Generated method info pointer for {proxyMethod.Name} doesn't point to any il2cpp method info");
        var methodInfo = UnityVersionHandler.Wrap((Il2CppMethodInfo*)methodInfoPointer);
        return methodInfo.MethodPointer;
    }

    #region Imports

    [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
    internal static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

    #endregion

    #region GenericMethod::GetMethod

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate Il2CppMethodInfo* d_GenericMethodGetMethod(Il2CppGenericMethod* gmethod, bool copyMethodPtr);

    private static readonly d_GenericMethodGetMethod GenericMethodGetMethodDetour =
        ClassInjector.hkGenericMethodGetMethod;

    internal static d_GenericMethodGetMethod GenericMethodGetMethod;
    internal static d_GenericMethodGetMethod GenericMethodGetMethodOriginal;

    private static d_GenericMethodGetMethod FindGenericMethodGetMethod()
    {
        var getVirtualMethodAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_object_get_virtual_method));
        Logger.Trace($"il2cpp_object_get_virtual_method: 0x{getVirtualMethodAPI.ToInt64():X2}");

        var getVirtualMethod = XrefScannerLowLevel.JumpTargets(getVirtualMethodAPI).Single();
        Logger.Trace($"Object::GetVirtualMethod: 0x{getVirtualMethod.ToInt64():X2}");

        var genericMethodGetMethod = XrefScannerLowLevel.JumpTargets(getVirtualMethod).Last();
        Logger.Trace($"GenericMethod::GetMethod: 0x{genericMethodGetMethod.ToInt64():X2}");

        var targetTargets = XrefScannerLowLevel.JumpTargets(genericMethodGetMethod).Take(2).ToList();
        if (targetTargets.Count == 1) // U2021.2.0+, there's additional shim that takes 3 parameters
            genericMethodGetMethod = targetTargets[0];
        GenericMethodGetMethodOriginal =
            ClassInjector.Detour.Detour(genericMethodGetMethod, GenericMethodGetMethodDetour);
        _delegateCache.Add(GenericMethodGetMethodDetour);
        _delegateCache.Add(GenericMethodGetMethodOriginal);
        return Marshal.GetDelegateForFunctionPointer<d_GenericMethodGetMethod>(genericMethodGetMethod);
    }

    #endregion

    #region Class::FromName

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate Il2CppClass* d_ClassFromName(Il2CppImage* image, IntPtr _namespace, IntPtr name);

    private static Il2CppClass* hkClassFromName(Il2CppImage* image, IntPtr _namespace, IntPtr name)
    {
        while (ClassFromNameOriginal == null) Thread.Sleep(1);
        var classPtr = ClassFromNameOriginal(image, _namespace, name);

        if (classPtr == null)
        {
            var namespaze = Marshal.PtrToStringAnsi(_namespace);
            var className = Marshal.PtrToStringAnsi(name);
            s_ClassNameLookup.TryGetValue((namespaze, className, (IntPtr)image), out var injectedClass);
            classPtr = (Il2CppClass*)injectedClass;
        }

        return classPtr;
    }

    private static readonly d_ClassFromName ClassFromNameDetour = hkClassFromName;
    internal static d_ClassFromName ClassFromName;
    internal static d_ClassFromName ClassFromNameOriginal;

    private static d_ClassFromName FindClassFromName()
    {
        var classFromNameAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_class_from_name));
        Logger.Trace($"il2cpp_class_from_name: 0x{classFromNameAPI.ToInt64():X2}");

        var classFromName = XrefScannerLowLevel.JumpTargets(classFromNameAPI).Single();
        Logger.Trace($"Class::FromName: 0x{classFromName.ToInt64():X2}");

        ClassFromNameOriginal = ClassInjector.Detour.Detour(classFromName, ClassFromNameDetour);
        _delegateCache.Add(ClassFromNameDetour);
        _delegateCache.Add(ClassFromNameOriginal);
        return Marshal.GetDelegateForFunctionPointer<d_ClassFromName>(classFromName);
    }

    #endregion

    #region MetadataCache::GetTypeInfoFromTypeDefinitionIndex

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate Il2CppClass* d_GetTypeInfoFromTypeDefinitionIndex(int index);

    private static Il2CppClass* hkGetTypeInfoFromTypeDefinitionIndex(int index)
    {
        if (s_InjectedClasses.TryGetValue(index, out var classPtr))
            return (Il2CppClass*)classPtr;

        while (GetTypeInfoFromTypeDefinitionIndexOriginal == null) Thread.Sleep(1);
        return GetTypeInfoFromTypeDefinitionIndexOriginal(index);
    }

    private static readonly d_GetTypeInfoFromTypeDefinitionIndex GetTypeInfoFromTypeDefinitionIndexDetour =
        hkGetTypeInfoFromTypeDefinitionIndex;

    internal static d_GetTypeInfoFromTypeDefinitionIndex GetTypeInfoFromTypeDefinitionIndex;
    internal static d_GetTypeInfoFromTypeDefinitionIndex GetTypeInfoFromTypeDefinitionIndexOriginal;

    private static d_GetTypeInfoFromTypeDefinitionIndex FindGetTypeInfoFromTypeDefinitionIndex()
    {
        var getTypeInfoFromTypeDefinitionIndex = IntPtr.Zero;

        // il2cpp_image_get_class is added in 2018.3.0f1
        if (UnityVersionHandler.UnityVersion < new Version(2018, 3, 0))
        {
            // (Kasuromi): RuntimeHelpers.InitializeArray calls an il2cpp icall, proxy function does some magic before it invokes it
            // https://github.com/Unity-Technologies/mono/blob/unity-2018.2/mcs/class/corlib/System.Runtime.CompilerServices/RuntimeHelpers.cs#L53-L54
            var runtimeHelpersInitializeArray = GetIl2CppMethodPointer(
                typeof(RuntimeHelpers)
                    .GetMethod("InitializeArray", new[] { typeof(Array), typeof(IntPtr) })
            );
            Logger.Trace(
                $"Il2CppSystem.Runtime.CompilerServices.RuntimeHelpers::InitializeArray: 0x{runtimeHelpersInitializeArray.ToInt64():X2}");

            var runtimeHelpersInitializeArrayICall =
                XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArray).Last();
            if (XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArrayICall).Count() == 1)
            {
                // is a thunk function
                Logger.Trace(
                    $"RuntimeHelpers::thunk_InitializeArray: 0x{runtimeHelpersInitializeArrayICall.ToInt64():X2}");
                runtimeHelpersInitializeArrayICall =
                    XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArrayICall).Single();
            }

            Logger.Trace($"RuntimeHelpers::InitializeArray: 0x{runtimeHelpersInitializeArrayICall.ToInt64():X2}");

            var typeGetUnderlyingType =
                XrefScannerLowLevel.JumpTargets(runtimeHelpersInitializeArrayICall).ElementAt(1);
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
                // (Kasuromi): Image::GetType appears to be inlined in il2cpp_image_get_class on some occasions,
                // if the unconditional xrefs are 0 then we are in the correct method (seen on unity 2019.3.15)
                getTypeInfoFromTypeDefinitionIndex = imageGetType;
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

        Logger.Trace(
            $"MetadataCache::GetTypeInfoFromTypeDefinitionIndex: 0x{getTypeInfoFromTypeDefinitionIndex.ToInt64():X2}");

        GetTypeInfoFromTypeDefinitionIndexOriginal = ClassInjector.Detour.Detour(
            getTypeInfoFromTypeDefinitionIndex,
            GetTypeInfoFromTypeDefinitionIndexDetour
        );
        _delegateCache.Add(GetTypeInfoFromTypeDefinitionIndexDetour);
        _delegateCache.Add(GetTypeInfoFromTypeDefinitionIndexOriginal);
        return Marshal.GetDelegateForFunctionPointer<d_GetTypeInfoFromTypeDefinitionIndex>(
            getTypeInfoFromTypeDefinitionIndex);
    }

    #endregion

    #region Class::FromIl2CppType

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate Il2CppClass* d_ClassFromIl2CppType(Il2CppTypeStruct* type);

    private static Il2CppClass* hkClassFromIl2CppType(Il2CppTypeStruct* type)
    {
        var wrappedType = UnityVersionHandler.Wrap(type);
        if ((long)wrappedType.Data < 0 && (wrappedType.Type == Il2CppTypeEnum.IL2CPP_TYPE_CLASS ||
                                            wrappedType.Type == Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE))
        {
            s_InjectedClasses.TryGetValue((long)wrappedType.Data, out var classPointer);
            return (Il2CppClass*)classPointer;
        }

        while (ClassFromIl2CppTypeOriginal == null) Thread.Sleep(1);
        return ClassFromIl2CppTypeOriginal(type);
    }

    private static readonly d_ClassFromIl2CppType ClassFromIl2CppTypeDetour = hkClassFromIl2CppType;
    internal static d_ClassFromIl2CppType ClassFromIl2CppType;
    internal static d_ClassFromIl2CppType ClassFromIl2CppTypeOriginal;

    private static d_ClassFromIl2CppType FindClassFromIl2CppType()
    {
        var classFromTypeAPI = GetIl2CppExport(nameof(IL2CPP.il2cpp_class_from_il2cpp_type));
        Logger.Trace($"il2cpp_class_from_il2cpp_type: 0x{classFromTypeAPI.ToInt64():X2}");

        var classFromType = XrefScannerLowLevel.JumpTargets(classFromTypeAPI).Single();
        Logger.Trace($"Class::FromIl2CppType: 0x{classFromType.ToInt64():X2}");

        ClassFromIl2CppTypeOriginal = ClassInjector.Detour.Detour(classFromType, ClassFromIl2CppTypeDetour);
        _delegateCache.Add(ClassFromIl2CppTypeDetour);
        _delegateCache.Add(ClassFromIl2CppTypeOriginal);
        return Marshal.GetDelegateForFunctionPointer<d_ClassFromIl2CppType>(classFromType);
    }

    #endregion

    #region Class::GetFieldDefaultValue

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate byte* d_ClassGetFieldDefaultValue(Il2CppFieldInfo* field, out Il2CppTypeStruct* type);

    private static byte* hkClassGetFieldDefaultValue(Il2CppFieldInfo* field, out Il2CppTypeStruct* type)
    {
        if (EnumInjector.GetDefaultValueOverride(field, out var newDefaultPtr))
        {
            var wrappedField = UnityVersionHandler.Wrap(field);
            var wrappedParent = UnityVersionHandler.Wrap(wrappedField.Parent);
            var wrappedElementClass = UnityVersionHandler.Wrap(wrappedParent.ElementClass);
            type = wrappedElementClass.ByValArg.TypePointer;
            return (byte*)newDefaultPtr;
        }

        while (ClassGetFieldDefaultValueOriginal == null) Thread.Sleep(1);
        return ClassGetFieldDefaultValueOriginal(field, out type);
    }

    private static readonly d_ClassGetFieldDefaultValue ClassGetFieldDefaultValueDetour = hkClassGetFieldDefaultValue;
    internal static d_ClassGetFieldDefaultValue ClassGetFieldDefaultValue;
    internal static d_ClassGetFieldDefaultValue ClassGetFieldDefaultValueOriginal;

    private static d_ClassGetFieldDefaultValue FindClassGetFieldDefaultValue()
    {
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

        ClassGetFieldDefaultValueOriginal =
            ClassInjector.Detour.Detour(classGetDefaultFieldValue, ClassGetFieldDefaultValueDetour);
        _delegateCache.Add(ClassGetFieldDefaultValueDetour);
        _delegateCache.Add(ClassGetFieldDefaultValueOriginal);
        return Marshal.GetDelegateForFunctionPointer<d_ClassGetFieldDefaultValue>(classGetDefaultFieldValue);
    }

    #endregion

    #region Class::Init

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void d_ClassInit(Il2CppClass* klass);

    internal static d_ClassInit ClassInit;

    private static readonly MemoryUtils.SignatureDefinition[] s_ClassInitSignatures =
    {
        new()
        {
            pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x28\x83",
            mask = "x????xxxxx",
            xref = true
        },
        new()
        {
            pattern = "\xE8\x00\x00\x00\x00\x0F\xB7\x47\x48\x48",
            mask = "x????xxxxx",
            xref = true
        }
    };

    private static d_ClassInit FindClassInit()
    {
        var pClassInit = s_ClassInitSignatures
            .Select(s => MemoryUtils.FindSignatureInModule(Il2CppModule, s))
            .FirstOrDefault(p => p != 0);

        if (pClassInit == 0)
        {
            // WARN: There might be a race condition with il2cpp_class_has_references
            Logger.Warning(
                "Class::Init signatures have been exhausted, using il2cpp_class_has_references as a substitute!");
            pClassInit = GetIl2CppExport(nameof(IL2CPP.il2cpp_class_has_references));
            if (pClassInit == 0)
            {
                Logger.Trace($"GameAssembly.dll: 0x{(long)Il2CppModule.BaseAddress}");
                throw new NotSupportedException(
                    "Failed to use signature for Class::Init and il2cpp_class_has_references cannot be found, please create an issue and report your unity version & game");
            }
        }

        Logger.Trace($"Class::Init: 0x{(long)pClassInit:X2}");

        return Marshal.GetDelegateForFunctionPointer<d_ClassInit>(pClassInit);
    }

    #endregion
}