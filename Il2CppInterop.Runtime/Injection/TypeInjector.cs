using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Injection.Hooks;
using Il2CppInterop.Runtime.Structs;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Class;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection;

// Note: This currently throws NotSupportedException for v108+ (Unity 6.6+) interfaces
public static unsafe class TypeInjector
{
    private static readonly IntPtr value__Cached = Marshal.StringToCoTaskMemUTF8("value__");
    /// <summary>
    /// fieldInfo : defaultValueBlob
    /// </summary>
    private static readonly ConcurrentDictionary<IntPtr, IntPtr> s_DefaultValueOverrides = new();
    private static readonly ConcurrentDictionary<(Type type, FieldAttributes attrs), IntPtr> _injectedFieldTypes = new();
    private static readonly HashSet<Type> RegisteredTypes = new();
    private static readonly HashSet<Type> NeedsInitialized = new();
    private static readonly Dictionary<Type, int> NeedsVTableSet = new();
    private static readonly HashSet<Type> NeedsFieldsSet = new();
    private static readonly ConcurrentDictionary<InvokerSignatureHash, Delegate> InvokerCache = new();

    /// <summary>
    /// If true, this type is part of the game and not something we are trying to inject nor something that has already been injected by us.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>True if the type is preexisting, false otherwise.</returns>
    [RequiresDynamicCode("")]
    public static bool IsPreexistingType(Type type)
    {
        return Il2CppType.GetClassPointer(type) is not 0 && !RegisteredTypes.Contains(type);
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public static void RegisterTypeInIl2Cpp<T>() where T : IIl2CppType<T>
    {
        RegisterTypeInIl2Cpp(typeof(T));
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static void RegisterTypeInIl2Cpp(Type type)
    {
        if (Il2CppType.GetClassPointer(type) is not 0)
        {
            // Already registered
            return;
        }
        // The above call guarantees that either:
        // * the static constructor is currently running and called this method.
        // * the static constructor is malformed and does not call this method.

        ValidateTypeUsingReflection(type);
        Hook.ApplyInjectionHooks();
        if (typeof(Il2CppSystem.IEnum).IsAssignableFrom(type))
        {
            RegisterEnumInIl2Cpp(type);
            return;
        }
        var vtableUpperBound = CalculateVTableUpperBoundUsingReflection(type);
        var classPointer = UnityVersionHandler.NewClass(vtableUpperBound);

        // Initialize as much of the class pointer as possible without touching other types.
        (var imageName, var @namespace, var name) = GetFullyQualifiedName(type);
        classPointer.Image = AssemblyInjector.GetOrCreateImage(imageName).ImagePointer;
        classPointer.Name = Marshal.StringToCoTaskMemUTF8(name);
        classPointer.Namespace = Marshal.StringToCoTaskMemUTF8(@namespace);
        classPointer.ElementClass = classPointer.Class = classPointer.CastClass = classPointer.ClassPointer;

        classPointer.NativeSize = -1;
        classPointer.ActualSize = classPointer.InstanceSize = 0;

        classPointer.Initialized = true;
        classPointer.InitializedAndNoError = true;
        classPointer.SizeInited = false;
        classPointer.HasFinalize = !type.IsValueType;
        classPointer.IsVtableInitialized = false;
        classPointer.ValueType = type.IsValueType;

        classPointer.ThisArg.Type = classPointer.ByValArg.Type = type.IsValueType ? Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE : Il2CppTypeEnum.IL2CPP_TYPE_CLASS;
        classPointer.ThisArg.Data = classPointer.ByValArg.Data = (nint)TokenAllocator.Assign(classPointer.Pointer);
        classPointer.ThisArg.ValueType = classPointer.ByValArg.ValueType = type.IsValueType;
        classPointer.ThisArg.ByRef = true;

        classPointer.Flags = TypeAttributesToClassAttributes(type.Attributes);

        // This guarantees that any future calls to GetClassPointer will return the class pointer,
        // even if the class is not fully registered yet, which allows us to handle circular dependencies between types.
        RegisteredTypes.Add(type);
        NeedsInitialized.Add(type);
        NeedsVTableSet.Add(type, vtableUpperBound);
        NeedsFieldsSet.Add(type);
        Il2CppType.SetClassPointer(type, (nint)classPointer.ClassPointer);
        Class_FromName_Hook.AddTypeToLookup(imageName, @namespace, name, (nint)classPointer.ClassPointer);

        // Ensure that all other types that this type depends on are at least registered
        {
            if (type is not { BaseType: null } and not { IsValueType: true })
            {
                EnsureNativeClassPointerNotNull(type.BaseType);
            }

            foreach (var interfaceType in type.GetInterfaces().Where(IsIl2CppInterface))
            {
                EnsureNativeClassPointerNotNull(interfaceType);
            }

            foreach (var (fieldType, _, _) in GetIl2CppFields(type))
            {
                EnsureNativeClassPointerNotNull(fieldType);
            }

            foreach (var property in GetIl2CppProperties(type))
            {
                EnsureNativeClassPointerNotNull(property.PropertyType);
            }

            foreach (var method in GetIl2CppMethods(type))
            {
                foreach (var methodType in GetMethodTypes(method))
                {
                    if (methodType.ContainsGenericParameters)
                        continue;
                    EnsureNativeClassPointerNotNull(methodType);
                }
            }
        }

        if (NeedsInitialized.Contains(type))
        {
            SetClassData(type, classPointer);
        }

        if (NeedsVTableSet.ContainsKey(type))
        {
            SetVTableAndInterfaces(type, classPointer, vtableUpperBound);
        }

        if (NeedsFieldsSet.Contains(type))
        {
            SetFields(type, classPointer);
        }

        static void EnsureNativeClassPointerNotNull(Type type) => GetClassPointerNotNull(type);

        static nint GetClassPointerNotNull(Type type)
        {
            var classPointer = Il2CppType.GetClassPointer(type);
            if (classPointer is 0)
            {
                Logger.Instance.LogWarning("The static constructor of {Type} is malformed and did not call RegisterTypeInIl2Cpp. Registering it now.", type.FullName);
                RegisterTypeInIl2Cpp(type);
                classPointer = Il2CppType.GetClassPointer(type);
                Debug.Assert(classPointer is not 0);
            }
            else if (!RegisteredTypes.Contains(type))
            {
                // Ensure that the vtable is initialized for this preexisting type
                ClassInitializer.Invoke((Il2CppClass*)classPointer);
            }
            return classPointer;
        }

        static void SetClassData(Type type, INativeClassStruct classPointer)
        {
            if (type.BaseType is not null && NeedsInitialized.Contains(type.BaseType))
            {
                SetClassData(type.BaseType, UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(type.BaseType)));
            }

            var baseClassPointer = type switch
            {
                { BaseType: null } => null,
                { IsValueType: true } => UnityVersionHandler.Wrap((Il2CppClass*)Il2CppType.GetClassPointer<Il2CppSystem.ValueType>()),
                _ => UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(type.BaseType)),
            };

            // Static classes get unsealed during generation so that they can be used as generic parameters, which might mislead users into thinking they can inherit from them.
            // This native check can be removed with changes to the generation, such as adding an attribute to indicate that a class is static and moving this check to the reflection validation.
            if (baseClassPointer is not null && (baseClassPointer.Flags & Il2CppClassAttributes.TYPE_ATTRIBUTE_SEALED) != 0)
                throw new ArgumentException($"Base class {type.BaseType} is sealed, so {type} can't inherit from it");

            // Initialize the rest of the class pointer now that it's visible to other types.
            classPointer.Parent = baseClassPointer?.ClassPointer;
            if (baseClassPointer is not null)
            {
                classPointer.ActualSize = classPointer.InstanceSize = baseClassPointer.InstanceSize;
                classPointer.SizeInited = true;
            }

            //var properties = GetIl2CppProperties(type).ToArray();

            var methods = GetIl2CppMethods(type).ToArray();
            var methodsOffset = type.IsInterface ? 0 : 1; // empty ctor
            var methodCount = methodsOffset + methods.Length;

            classPointer.MethodCount = (ushort)methodCount;
            var methodPointerArray = (Il2CppMethodInfo**)Marshal.AllocHGlobal(methodCount * IntPtr.Size);
            classPointer.Methods = methodPointerArray;

            if (!type.IsInterface)
            {
                methodPointerArray[0] = CreateEmptyCtor(classPointer);
            }

            for (var i = 0; i < methods.Length; i++)
            {
                var methodInfo = methods[i];
                methodPointerArray[i + methodsOffset] = ConvertMethodInfo(methodInfo, classPointer);
            }

            NeedsInitialized.Remove(type);
        }

        static void SetVTableAndInterfaces(Type type, INativeClassStruct classPointer, int vtableAllocatedSize)
        {
            if (NeedsInitialized.Contains(type))
            {
                SetClassData(type, classPointer);
            }
            Type[] interfaceTypesNotImplementedByBaseType;
            INativeClassStruct? baseClassPointer;
            Type? baseType;
            if (type is { BaseType: null })
            {
                baseClassPointer = null;
                baseType = null;
                interfaceTypesNotImplementedByBaseType = type.GetInterfaces().Where(IsIl2CppInterface).ToArray();
            }
            else
            {
                baseType = type.IsValueType ? typeof(Il2CppSystem.ValueType) : type.BaseType;
                interfaceTypesNotImplementedByBaseType = type.GetInterfaces().Where(IsIl2CppInterface).Where(i => !i.IsAssignableFrom(baseType)).ToArray();
                baseClassPointer = UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(baseType));
                if (NeedsVTableSet.TryGetValue(baseType, out var baseTypeVTableMaxSize))
                {
                    SetVTableAndInterfaces(baseType, baseClassPointer, baseTypeVTableMaxSize);
                }
            }
            foreach (var @interface in interfaceTypesNotImplementedByBaseType)
            {
                if (NeedsVTableSet.TryGetValue(@interface, out var interfaceVTableMaxSize))
                {
                    var interfaceClassPointer = UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(@interface));
                    SetVTableAndInterfaces(@interface, interfaceClassPointer, interfaceVTableMaxSize);
                }
            }

            if (baseClassPointer is null)
            {
                classPointer.InterfaceCount = (ushort)interfaceTypesNotImplementedByBaseType.Length;
                classPointer.ImplementedInterfaces = (Il2CppClass**)Marshal.AllocHGlobal(interfaceTypesNotImplementedByBaseType.Length * IntPtr.Size);
                for (var i = 0; i < interfaceTypesNotImplementedByBaseType.Length; i++)
                {
                    var interfaceType = interfaceTypesNotImplementedByBaseType[i];
                    classPointer.ImplementedInterfaces[i] = (Il2CppClass*)GetClassPointerNotNull(interfaceType);
                }
            }
            else
            {
                classPointer.InterfaceCount = (ushort)(interfaceTypesNotImplementedByBaseType.Length + baseClassPointer.InterfaceCount);
                classPointer.ImplementedInterfaces = (Il2CppClass**)Marshal.AllocHGlobal(classPointer.InterfaceCount * IntPtr.Size);
                Buffer.MemoryCopy(baseClassPointer.ImplementedInterfaces, classPointer.ImplementedInterfaces, classPointer.InterfaceCount * IntPtr.Size, baseClassPointer.InterfaceCount * IntPtr.Size);
                for (var i = 0; i < interfaceTypesNotImplementedByBaseType.Length; i++)
                {
                    var interfaceType = interfaceTypesNotImplementedByBaseType[i];
                    classPointer.ImplementedInterfaces[baseClassPointer.InterfaceCount + i] = (Il2CppClass*)GetClassPointerNotNull(interfaceType);
                }
            }

            var pointersToInterfaces = type.GetInterfaces().Where(IsIl2CppInterface).ToDictionary(Il2CppType.GetClassPointer);

            var interfaceOffsets = new List<Il2CppRuntimeInterfaceOffsetPair>();

            var map1 = CreateMethodInfoToHashDictionary(type);
            var map4 = CreateHashToNativeMethodInfoDictionary(classPointer);

            var index = 0;
            if (baseClassPointer is not null)
            {
                Debug.Assert(baseType is not null);

                var baseMethodToMethodMap = GetIl2CppMethods(type).ToDictionary(m => m.GetBaseDefinition(), m => m);

                var baseMap2 = CreateHashToMethodInfoDictionary(baseType);
                var baseMap3 = CreateNativeMethodInfoToHashDictionary(baseClassPointer);

                var lowestedInterfaceOffset = LowestInterfaceOffset(baseClassPointer);
                for (; index < lowestedInterfaceOffset; index++)
                {
                    ThrowIfNotEnoughAllocated(index, vtableAllocatedSize, type);
                    var vtableEntry = classPointer.VTable + index;
                    var baseVTableEntry = baseClassPointer.VTable + index;
                    var baseMethodInfo = baseMap2[baseMap3[(nint)baseVTableEntry->method]];
                    if (baseMethodToMethodMap.TryGetValue(baseMethodInfo, out var methodInfo))
                    {
                        var nativeMethodInfo = map4[map1[methodInfo]];
                        vtableEntry->methodPtr = nativeMethodInfo.MethodPointer;
                        vtableEntry->method = nativeMethodInfo.MethodInfoPointer;
                    }
                    else
                    {
                        *vtableEntry = *baseVTableEntry;
                    }
                }

                // Virtual methods declared in this class
                foreach (var methodInfo in baseMethodToMethodMap.Values)
                {
                    // If the method is not overridden, it will be a key in the dictionary.
                    if (!baseMethodToMethodMap.ContainsKey(methodInfo))
                        continue;

                    if (!methodInfo.IsAbstract && !methodInfo.IsVirtual)
                        continue;

                    if (methodInfo.IsFinal)
                        continue;

                    var nativeMethodInfo = map4[map1[methodInfo]];
                    ThrowIfNotEnoughAllocated(index, vtableAllocatedSize, type);
                    classPointer.VTable[index] = new()
                    {
                        methodPtr = nativeMethodInfo.MethodPointer,
                        method = nativeMethodInfo.MethodInfoPointer
                    };
                    index++;
                }

                for (var interfaceIndex = 0; interfaceIndex < baseClassPointer.InterfaceOffsetsCount; interfaceIndex++)
                {
                    var pair = baseClassPointer.InterfaceOffsets[interfaceIndex];
                    var interfaceClassPointer = UnityVersionHandler.Wrap(pair.interfaceType);
                    var interfaceType = pointersToInterfaces[interfaceClassPointer.Pointer];
                    var baseInterfaceOffset = pair.offset;
                    var interfaceOffset = index;
                    int interfaceVtableCount = interfaceClassPointer.VTableCount;

                    interfaceOffsets.Add(new Il2CppRuntimeInterfaceOffsetPair
                    {
                        interfaceType = pair.interfaceType,
                        offset = interfaceOffset
                    });

                    Dictionary<MethodInfo, MethodInfo> interfaceMethodToImplementingMethod;
                    {
                        var interfaceMapStruct = type.GetInterfaceMap(interfaceType);
                        interfaceMethodToImplementingMethod = new Dictionary<MethodInfo, MethodInfo>(interfaceMapStruct.InterfaceMethods.Length);
                        for (var i = 0; i < interfaceMapStruct.InterfaceMethods.Length; i++)
                        {
                            interfaceMethodToImplementingMethod.Add(interfaceMapStruct.InterfaceMethods[i], interfaceMapStruct.TargetMethods[i]);
                        }
                    }

                    var interfaceMap2 = CreateHashToMethodInfoDictionary(interfaceType);
                    var interfaceMap3 = CreateNativeMethodInfoToHashDictionary(interfaceClassPointer);

                    for (var i = 0; i < interfaceVtableCount; i++)
                    {
                        Debug.Assert(index == interfaceOffset + i);
                        ThrowIfNotEnoughAllocated(index, vtableAllocatedSize, type);
                        var vtableEntry = classPointer.VTable + interfaceOffset + i;
                        var interfaceVTableEntry = interfaceClassPointer.VTable + i;
                        var interfaceMethodInfo = interfaceMap2[interfaceMap3[(nint)interfaceVTableEntry->method]];
                        if (interfaceMethodToImplementingMethod.TryGetValue(interfaceMethodInfo, out var methodInfo) && methodInfo.DeclaringType == type)
                        {
                            var nativeMethodInfo = map4[map1[methodInfo]];
                            vtableEntry->methodPtr = nativeMethodInfo.MethodPointer;
                            vtableEntry->method = nativeMethodInfo.MethodInfoPointer;
                        }
                        else
                        {
                            var baseVTableEntry = baseClassPointer.VTable + baseInterfaceOffset + i;
                            *vtableEntry = *baseVTableEntry;
                        }
                        index++;
                    }
                }
            }

            foreach (var interfaceType in interfaceTypesNotImplementedByBaseType)
            {
                var interfaceClassPointer = UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(interfaceType));
                var interfaceOffset = index;
                var interfaceVtableCount = LowestInterfaceOffset(interfaceClassPointer); // Not sure this is correct

                if (interfaceVtableCount == 0)
                    continue;

                interfaceOffsets.Add(new Il2CppRuntimeInterfaceOffsetPair
                {
                    interfaceType = interfaceClassPointer.ClassPointer,
                    offset = interfaceOffset
                });

                Dictionary<MethodInfo, MethodInfo> interfaceMethodToImplementingMethod;
                {
                    var interfaceMapStruct = type.GetInterfaceMap(interfaceType);
                    interfaceMethodToImplementingMethod = new Dictionary<MethodInfo, MethodInfo>(interfaceMapStruct.InterfaceMethods.Length);
                    for (var i = 0; i < interfaceMapStruct.InterfaceMethods.Length; i++)
                    {
                        interfaceMethodToImplementingMethod.Add(interfaceMapStruct.InterfaceMethods[i], interfaceMapStruct.TargetMethods[i]);
                    }
                }

                var interfaceMap2 = CreateHashToMethodInfoDictionary(interfaceType);
                var interfaceMap3 = CreateNativeMethodInfoToHashDictionary(interfaceClassPointer);

                for (var i = 0; i < interfaceVtableCount; i++)
                {
                    Debug.Assert(index == interfaceOffset + i);
                    ThrowIfNotEnoughAllocated(index, vtableAllocatedSize, type);
                    var vtableEntry = classPointer.VTable + interfaceOffset + i;
                    var interfaceVTableEntry = interfaceClassPointer.VTable + i;
                    var interfaceMethodInfo = interfaceMap2[interfaceMap3[(nint)interfaceVTableEntry->method]];
                    var methodInfo = interfaceMethodToImplementingMethod[interfaceMethodInfo];
                    var nativeMethodInfo = map4[map1[methodInfo]];
                    vtableEntry->methodPtr = nativeMethodInfo.MethodPointer;
                    vtableEntry->method = nativeMethodInfo.MethodInfoPointer;
                    index++;
                }
            }

            classPointer.InterfaceOffsetsCount = (ushort)interfaceOffsets.Count;
            classPointer.InterfaceOffsets = (Il2CppRuntimeInterfaceOffsetPair*)Marshal.AllocHGlobal(interfaceOffsets.Count * sizeof(Il2CppRuntimeInterfaceOffsetPair));
            for (var i = 0; i < interfaceOffsets.Count; i++)
            {
                classPointer.InterfaceOffsets[i] = interfaceOffsets[i];
            }

            classPointer.IsVtableInitialized = true;
            NeedsVTableSet.Remove(type);

            static void ThrowIfNotEnoughAllocated(int index, int allocatedSize, Type type)
            {
                if (index >= allocatedSize)
                    throw new InvalidOperationException($"Not enough vtable space allocated for type {type}. Allocated: {allocatedSize}");
            }
        }

        static void SetFields(Type type, INativeClassStruct classPointer)
        {
            if (NeedsInitialized.Contains(type))
            {
                SetClassData(type, classPointer);
            }
            foreach (var fieldType in GetIl2CppInstanceFieldTypes(type))
            {
                if (fieldType.IsValueType && NeedsFieldsSet.Contains(fieldType))
                {
                    SetFields(fieldType, UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(fieldType)));
                }
            }

            if (type.BaseType is not null && NeedsFieldsSet.Contains(type.BaseType))
            {
                SetFields(type.BaseType, UnityVersionHandler.Wrap((Il2CppClass*)GetClassPointerNotNull(type.BaseType)));
            }

            var fieldsToInject = GetIl2CppFields(type).ToArray();
            classPointer.FieldCount = (ushort)fieldsToInject.Length;

            var il2cppFields = (Il2CppFieldInfo*)Marshal.AllocHGlobal(classPointer.FieldCount * UnityVersionHandler.FieldInfoSize());
            var fieldOffset = (int)classPointer.InstanceSize;
            for (var i = 0; i < classPointer.FieldCount; i++)
            {
                var (fieldType, fieldAttributes, fieldName) = fieldsToInject[i];

                var fieldInfoClass = Il2CppType.GetClassPointer(fieldType);
                if (fieldInfoClass == IntPtr.Zero)
                    throw new Exception($"Type {fieldType} in {type}.{fieldName} doesn't exist in Il2Cpp");
                if (!_injectedFieldTypes.TryGetValue((fieldType, fieldAttributes), out var fieldTypePtr))
                {
                    var classType =
                        UnityVersionHandler.Wrap((Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(fieldInfoClass));

                    var duplicatedType = UnityVersionHandler.NewType();
                    duplicatedType.Data = classType.Data;
                    duplicatedType.Attrs = (ushort)fieldAttributes;
                    duplicatedType.Type = classType.Type;
                    duplicatedType.ByRef = classType.ByRef;
                    duplicatedType.Pinned = classType.Pinned;

                    _injectedFieldTypes[(fieldType, fieldAttributes)] = duplicatedType.Pointer;
                    fieldTypePtr = duplicatedType.Pointer;
                }

                var fieldInfo = UnityVersionHandler.Wrap(il2cppFields + i * UnityVersionHandler.FieldInfoSize());
                fieldInfo.Name = Marshal.StringToCoTaskMemUTF8(fieldName);
                fieldInfo.Parent = classPointer.ClassPointer;
                fieldInfo.Type = (Il2CppTypeStruct*)fieldTypePtr;

                if (fieldAttributes.HasFlag(FieldAttributes.Static))
                {
                    fieldInfo.Offset = 0;
                }
                else
                {
                    fieldInfo.Offset = fieldOffset;
                    if (IL2CPP.il2cpp_class_is_valuetype(fieldInfoClass))
                    {
                        var fieldSize = IL2CPP.il2cpp_class_value_size(fieldInfoClass, out _);
                        fieldOffset += fieldSize;
                    }
                    else
                    {
                        fieldOffset += sizeof(Il2CppObject*);
                    }
                }
            }

            classPointer.Fields = il2cppFields;

            classPointer.InstanceSize = (uint)fieldOffset;
            classPointer.ActualSize = classPointer.InstanceSize;
            classPointer.SizeInited = true;

            NeedsFieldsSet.Remove(type);
        }
    }

    private static int LowestInterfaceOffset(INativeClassStruct classPointer)
    {
        int result = classPointer.VTableCount;
        for (var i = classPointer.InterfaceOffsetsCount - 1; i >= 0; i--)
        {
            var offset = (classPointer.InterfaceOffsets + i)->offset;
            if (offset < result)
                result = offset;
        }
        return result;
    }

    [RequiresDynamicCode("")]
    private static Dictionary<NamedSignatureHash, MethodInfo> CreateHashToMethodInfoDictionary([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllMethods)] Type type)
    {
        return GetAllIl2CppMethods(type).ToDictionary(m => new NamedSignatureHash(m));
    }

    [RequiresDynamicCode("")]
    private static Dictionary<MethodInfo, NamedSignatureHash> CreateMethodInfoToHashDictionary([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllMethods)] Type type)
    {
        return GetAllIl2CppMethods(type).ToDictionary(m => m, m => new NamedSignatureHash(m));
    }

    private static Dictionary<NamedSignatureHash, INativeMethodInfoStruct> CreateHashToNativeMethodInfoDictionary(INativeClassStruct classPointer)
    {
        var dict = new Dictionary<NamedSignatureHash, INativeMethodInfoStruct>(classPointer.MethodCount);
        var currentClassPointer = classPointer;
        while (true)
        {
            for (var i = 0; i < currentClassPointer.MethodCount; i++)
            {
                var methodInfo = UnityVersionHandler.Wrap(currentClassPointer.Methods[i]);
                dict.TryAdd(new NamedSignatureHash(methodInfo), methodInfo);
            }
            if (currentClassPointer.Parent is null)
                break;
            currentClassPointer = UnityVersionHandler.Wrap(currentClassPointer.Parent);
        }
        return dict;
    }

    private static Dictionary<IntPtr, NamedSignatureHash> CreateNativeMethodInfoToHashDictionary(INativeClassStruct classPointer)
    {
        var dict = new Dictionary<IntPtr, NamedSignatureHash>(classPointer.MethodCount);
        var currentClassPointer = classPointer;
        while (true)
        {
            for (var i = 0; i < currentClassPointer.MethodCount; i++)
            {
                var methodInfo = UnityVersionHandler.Wrap(currentClassPointer.Methods[i]);
                dict.Add(methodInfo.Pointer, new NamedSignatureHash(methodInfo));
            }
            if (currentClassPointer.Parent is null)
                break;
            currentClassPointer = UnityVersionHandler.Wrap(currentClassPointer.Parent);
        }
        return dict;
    }

    private static IEnumerable<MethodInfo> GetAllIl2CppMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.AllMethods)] Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static).Where(IsIl2CppMethod);
    }

    private static IEnumerable<MethodInfo> GetIl2CppMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(IsIl2CppMethod);
    }

    private static IEnumerable<PropertyInfo> GetIl2CppProperties([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties)] Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(IsIl2CppProperty);
    }

    private static IEnumerable<(Type FieldType, FieldAttributes Attributes, string Name)> GetIl2CppFields([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type type)
    {
        List<(Type, FieldAttributes, string)> fields = new();
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
        {
            if (IsIl2CppField(property))
            {
                var fieldAttribute = property.GetCustomAttribute<Il2CppFieldAttribute>();
                var fieldName = fieldAttribute?.Name ?? property.Name;
                var fieldType = property.PropertyType;
                FieldAttributes fieldAttributes = default;
                if (property.IsStatic())
                {
                    fieldAttributes |= FieldAttributes.Static;
                }
                if (fieldName.EndsWith(">k__BackingField", StringComparison.Ordinal))
                {
                    fieldAttributes |= FieldAttributes.SpecialName;
                    fieldAttributes |= FieldAttributes.Private;
                }
                else
                {
                    fieldAttributes |= FieldAttributes.Public;
                }
                fields.Add((fieldType, fieldAttributes, fieldName));
            }
        }
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            if (IsIl2CppField(field))
            {
                var fieldAttribute = field.GetCustomAttribute<Il2CppFieldAttribute>();
                var fieldName = fieldAttribute?.Name ?? field.Name;
                var fieldType = field.FieldType;
                var fieldAttributes = field.Attributes;
                fields.Add((fieldType, fieldAttributes, fieldName));
            }
        }
        return fields;
    }

    private static IEnumerable<Type> GetIl2CppInstanceFieldTypes([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties | DynamicallyAccessedMemberTypes.NonPublicProperties | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type type)
    {
        return GetIl2CppFields(type).Where(f => !f.Attributes.HasFlag(FieldAttributes.Static)).Select(f => f.FieldType);
    }

    [RequiresDynamicCode("Calls System.Type.MakeGenericType(params Type[])")]
    private static void ValidateTypeUsingReflection([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type type)
    {
        // Enums are transformed into structs during generation
        if (type.IsEnum)
            throw new ArgumentException("Enums must be written as structs to be registered in Il2Cpp. Use the IEnum interface to mark them as enums.", nameof(type));

        // Injected types cannot be generic
        if (type.IsGenericType)
            throw new ArgumentException("Generic types cannot be registered in Il2Cpp.", nameof(type));

        if (type.DeclaringType is not null)
            throw new ArgumentException("Nested types cannot be registered in Il2Cpp.", nameof(type));

        // Types must inherit from IIl2CppType
        if (!typeof(IIl2CppType).IsAssignableFrom(type))
            throw new ArgumentException("Types must implement IIl2CppType to be registered in Il2Cpp.", nameof(type));

        if (type.IsValueType)
        {
            // Value types must inherit from IValueType
            if (!typeof(Il2CppSystem.IValueType).IsAssignableFrom(type))
                throw new ArgumentException("Value types must implement IValueType to be registered in Il2Cpp.", nameof(type));

            // Enums must have a single instance field named "value__" that represents the underlying value of the enum
            if (typeof(Il2CppSystem.IEnum).IsAssignableFrom(type))
            {
                var instanceFields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (instanceFields.Length != 1)
                    throw new ArgumentException("Enums must have a single instance field that represents the underlying value of the enum to be registered in Il2Cpp.", nameof(type));

                var instanceField = instanceFields[0];
                if (instanceField.Name != "value__")
                    throw new ArgumentException("Enums must have a single instance field named \"value__\" to be registered in Il2Cpp.", nameof(type));
            }
        }
        else if (type.IsClass)
        {
            // Class types must inherit from Il2CppSystem.Object
            if (!typeof(Il2CppSystem.Object).IsAssignableFrom(type))
                throw new ArgumentException("Class types must inherit from Il2CppSystem.Object to be registered in Il2Cpp.", nameof(type));

            if (typeof(Il2CppSystem.IValueType).IsAssignableFrom(type))
                throw new ArgumentException("Class types cannot implement IValueType to be registered in Il2Cpp.", nameof(type));

            Debug.Assert(type.BaseType is not null);

            // The base type of class types cannot be generic
            if (type.BaseType.IsGenericType)
                throw new ArgumentException("The base type of class types cannot be generic to be registered in Il2Cpp.", nameof(type));

            // The type must have a constructor that takes a ObjectPointer parameter
            if (type.GetConstructor([typeof(ObjectPointer)]) is null)
                throw new ArgumentException("Class types must have a constructor that takes an ObjectPointer parameter to be registered in Il2Cpp.", nameof(type));
        }

        // Types must inherit from IIl2CppType<T> where T is the type itself
        try
        {
            // This will throw if the type does not implement IIl2CppType<T> where T is the type itself
            // because T has a self-referential constraint that cannot be satisfied if the type does not implement the interface correctly.
            // If https://github.com/dotnet/runtime/issues/28033 is implemented, we can replace this with a more direct check.
            typeof(IIl2CppType<>).MakeGenericType(type);
        }
        catch
        {
            throw new ArgumentException("Types must implement IIl2CppType<T> where T is the type itself to be registered in Il2Cpp.", nameof(type));
        }
    }

    [RequiresUnreferencedCode("")]
    private static int CalculateVTableUpperBoundUsingReflection([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
    {
        var count = 0;
        foreach (var interfaceType in type.GetInterfaces().Where(IsIl2CppInterface))
        {
            count += CountIl2CppMethods(interfaceType);
        }
        var currentType = type;
        while (currentType is not null)
        {
            count += CountIl2CppMethods(currentType);
            currentType = currentType.BaseType;
        }
        if (type.IsValueType)
        {
            count += UnityVersionHandler.Wrap((Il2CppClass*)Il2CppType.GetClassPointer<Il2CppSystem.ValueType>()).VTableCount;
        }
        return int.Min(count, ushort.MaxValue);

        static int CountIl2CppMethods([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.NonPublicMethods)] Type type)
        {
            return type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Count(IsIl2CppMethod);
        }
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public static void InjectEnumValues<TEnum>(Dictionary<string, object> valuesToAdd) where TEnum : Il2CppSystem.IEnum
    {
        InjectEnumValues(typeof(TEnum), valuesToAdd);
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    public static void InjectEnumValues(Type type, Dictionary<string, object> valuesToAdd)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!typeof(Il2CppSystem.IEnum).IsAssignableFrom(type))
            throw new ArgumentException("Type argument needs to be an enum", nameof(type));

        var enumPtr = Il2CppType.GetClassPointer(type);
        if (enumPtr == IntPtr.Zero)
            throw new ArgumentException("Type needs to be an Il2Cpp enum", nameof(type));

        Hook.ApplyInjectionHooks();

        ClassInitializer.Invoke((Il2CppClass*)enumPtr);

        var il2cppEnum = UnityVersionHandler.Wrap((Il2CppClass*)enumPtr);
        var newFieldCount = il2cppEnum.FieldCount + valuesToAdd.Count;
        var newFields = (Il2CppFieldInfo*)Marshal.AllocHGlobal(newFieldCount * UnityVersionHandler.FieldInfoSize());

        int fieldIdx;
        for (fieldIdx = 0; fieldIdx < il2cppEnum.FieldCount; ++fieldIdx)
        {
            var offset = fieldIdx * UnityVersionHandler.FieldInfoSize();
            var oldField = UnityVersionHandler.Wrap(il2cppEnum.Fields + offset);
            var newField = UnityVersionHandler.Wrap(newFields + offset);

            newField.Name = oldField.Name;
            newField.Type = oldField.Type;
            newField.Parent = oldField.Parent;
            newField.Offset = oldField.Offset;

            // Move the default value blob from the old field to the new one
            if (s_DefaultValueOverrides.TryRemove((IntPtr)oldField.FieldInfoPointer, out var blob))
                s_DefaultValueOverrides[(IntPtr)newField.FieldInfoPointer] = blob;
        }

        var enumElementType = UnityVersionHandler.Wrap(il2cppEnum.ElementClass).ByValArg;

        foreach (var newData in valuesToAdd)
        {
            var offset = fieldIdx * UnityVersionHandler.FieldInfoSize();
            var newField = UnityVersionHandler.Wrap(newFields + offset);
            newField.Name = Marshal.StringToCoTaskMemUTF8(newData.Key);
            newField.Type = enumElementType.TypePointer;
            newField.Parent = il2cppEnum.ClassPointer;
            newField.Offset = 0;

            CreateOrUpdateFieldDefaultValue(newField.FieldInfoPointer, enumElementType.TypePointer, newData.Value);

            ++fieldIdx;
        }

        il2cppEnum.FieldCount = (ushort)newFieldCount;
        il2cppEnum.Fields = newFields;

        if (TypeFromClassPointer(enumPtr, type.FullName) is Il2CppSystem.RuntimeType runtimeEnumType)
            // The mono runtime caches the enum names and values the first time they are requested, so we reset this cache
            runtimeEnumType.GenericCache = null;

        static Il2CppSystem.Type TypeFromClassPointer(nint classPointer, string? typeName)
        {
            if (classPointer == IntPtr.Zero)
            {
                throw new ArgumentException($"{typeName} does not have a corresponding IL2CPP class pointer");
            }

            var il2CppType = IL2CPP.il2cpp_class_get_type(classPointer);
            if (il2CppType == IntPtr.Zero)
            {
                throw new ArgumentException($"{typeName} does not have a corresponding IL2CPP type pointer");
            }

            return Il2CppSystem.Type.FromTypePointer(il2CppType);
        }
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static void RegisterEnumInIl2Cpp(Type type)
    {
        var baseEnum = UnityVersionHandler.Wrap((Il2CppClass*)Il2CppType.GetClassPointer<Il2CppSystem.Enum>());

        ClassInitializer.Invoke(baseEnum.ClassPointer);

        var il2cppEnum = UnityVersionHandler.NewClass(baseEnum.VTableCount);
        var elementClass =
            UnityVersionHandler.Wrap(
                (Il2CppClass*)Il2CppType.GetClassPointer(GetEnumUnderlyingType(type)));

        (var imageName, var @namespace, var name) = GetFullyQualifiedName(type);
        il2cppEnum.Image = AssemblyInjector.GetOrCreateImage(imageName).ImagePointer;
        il2cppEnum.Class = il2cppEnum.CastClass = il2cppEnum.ElementClass = elementClass.ClassPointer;
        il2cppEnum.Parent = baseEnum.ClassPointer;
        il2cppEnum.ActualSize = il2cppEnum.InstanceSize =
            (uint)(baseEnum.InstanceSize + GetEnumElementSize(elementClass.ByValArg.Type));
        il2cppEnum.NativeSize = -1;

        il2cppEnum.ValueType = true;
        il2cppEnum.EnumType = true;
        il2cppEnum.Initialized = true;
        il2cppEnum.InitializedAndNoError = true;
        il2cppEnum.SizeInited = true;
        il2cppEnum.HasFinalize = true;
        il2cppEnum.IsVtableInitialized = true;

        il2cppEnum.Name = Marshal.StringToCoTaskMemUTF8(name);
        il2cppEnum.Namespace = Marshal.StringToCoTaskMemUTF8(@namespace);

        il2cppEnum.ThisArg.Data = il2cppEnum.ByValArg.Data = (nint)TokenAllocator.Assign(il2cppEnum.Pointer);

        // Has to be IL2CPP_TYPE_VALUETYPE because IL2CPP_TYPE_ENUM isn't used
        il2cppEnum.ThisArg.Type = il2cppEnum.ByValArg.Type = Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE;

        il2cppEnum.Flags = (Il2CppClassAttributes)type.Attributes;

        il2cppEnum.VTableCount = baseEnum.VTableCount;
        var vtable = il2cppEnum.VTable;
        var baseVTable = baseEnum.VTable;
        for (var i = 0; i < baseEnum.VTableCount; i++)
            vtable[i] = baseVTable[i];

        var enumNamesAndValues = GetEnumNamesAndValues(type);
        il2cppEnum.FieldCount = (ushort)(enumNamesAndValues.Length + 1); // value__

        var il2cppFields =
            (Il2CppFieldInfo*)Marshal.AllocHGlobal(il2cppEnum.FieldCount * UnityVersionHandler.FieldInfoSize());
        var valueField = UnityVersionHandler.Wrap(il2cppFields);
        valueField.Name = value__Cached;
        valueField.Parent = il2cppEnum.ClassPointer;
        valueField.Offset = (int)baseEnum.InstanceSize;

        var enumValueType = UnityVersionHandler.NewType();
        enumValueType.Data = elementClass.ThisArg.Data;
        enumValueType.Attrs = (ushort)(FieldAttributes.Private | FieldAttributes.Family | FieldAttributes.SpecialName |
                                        FieldAttributes.RTSpecialName);
        enumValueType.Type = elementClass.ThisArg.Type;
        enumValueType.ByRef = elementClass.ThisArg.ByRef;
        enumValueType.Pinned = elementClass.ThisArg.Pinned;

        valueField.Type = enumValueType.TypePointer;

        var enumConstType = UnityVersionHandler.NewType();
        enumConstType.Data = il2cppEnum.ThisArg.Data;
        enumConstType.Attrs = (ushort)(FieldAttributes.Private | FieldAttributes.Family | FieldAttributes.InitOnly |
                                        FieldAttributes.Literal | FieldAttributes.HasDefault);
        enumConstType.Type = Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE;
        enumConstType.ByRef = false;
        enumConstType.Pinned = false;

        for (var i = 1; i < il2cppEnum.FieldCount; i++)
        {
            var (fieldName, fieldValue) = enumNamesAndValues[i - 1];
            var il2cppField = UnityVersionHandler.Wrap(il2cppFields + i * UnityVersionHandler.FieldInfoSize());
            il2cppField.Name = Marshal.StringToCoTaskMemUTF8(fieldName);
            il2cppField.Type = enumConstType.TypePointer;
            il2cppField.Parent = il2cppEnum.ClassPointer;
            il2cppField.Offset = 0;

            CreateOrUpdateFieldDefaultValue(il2cppField.FieldInfoPointer, elementClass.ThisArg.TypePointer, fieldValue);
        }

        il2cppEnum.Fields = il2cppFields;

        il2cppEnum.TypeHierarchyDepth = (byte)(1 + baseEnum.TypeHierarchyDepth);
        il2cppEnum.TypeHierarchy = (Il2CppClass**)Marshal.AllocHGlobal(il2cppEnum.TypeHierarchyDepth * sizeof(void*));
        for (var i = 0; i < il2cppEnum.TypeHierarchyDepth; i++)
            il2cppEnum.TypeHierarchy[i] = baseEnum.TypeHierarchy[i];
        il2cppEnum.TypeHierarchy[il2cppEnum.TypeHierarchyDepth - 1] = il2cppEnum.ClassPointer;

        Il2CppType.SetClassPointer(type, il2cppEnum.Pointer);
        Class_FromName_Hook.AddTypeToLookup(imageName, @namespace, name, il2cppEnum.Pointer);
    }

    private static bool IsIl2CppInterface(Type type)
    {
        return type.IsInterface
            && typeof(Il2CppSystem.IObject).IsAssignableFrom(type)
            && type != typeof(Il2CppSystem.IObject)
            && type != typeof(Il2CppSystem.IValueType)
            && type != typeof(Il2CppSystem.IEnum);
    }

    private static bool IsIl2CppField(PropertyInfo property)
    {
        // Has Il2CppFieldAttribute
        return property.GetCustomAttribute<Il2CppFieldAttribute>() is not null;
    }

    private static bool IsIl2CppField(FieldInfo field)
    {
        // Has Il2CppFieldAttribute
        return field.GetCustomAttribute<Il2CppFieldAttribute>() is not null;
    }

    private static bool IsIl2CppProperty(PropertyInfo property)
    {
        // Has Il2CppPropertyAttribute
        return property.GetCustomAttribute<Il2CppPropertyAttribute>() is not null;
    }

    private static bool IsIl2CppMethod(MethodInfo method)
    {
        // Has Il2CppMethodAttribute
        return method.GetCustomAttribute<Il2CppMethodAttribute>() is not null && !method.ContainsGenericParameters && !method.IsConstructor;
    }

    private static IEnumerable<Type> GetMethodTypes(MethodInfo method)
    {
        foreach (var parameter in method.GetParameters())
        {
            yield return parameter.ParameterType;
        }
        if (method.ReturnType != typeof(void))
        {
            yield return method.ReturnType;
        }
    }

    private static Il2CppMethodInfo* CreateEmptyCtor(INativeClassStruct declaringClass)
    {
        var converted = UnityVersionHandler.NewMethod();
        converted.Name = Marshal.StringToCoTaskMemUTF8(".ctor");
        converted.Class = declaringClass.ClassPointer;

        void* invoker;
        if (UnityVersionHandler.IsMetadataV29OrHigher)
        {
            invoker = (delegate* unmanaged<IntPtr, Il2CppMethodInfo*, IntPtr, IntPtr*, IntPtr*, void>)&Invoker_MetadataV29;
        }
        else
        {
            invoker = (delegate* unmanaged<IntPtr, Il2CppMethodInfo*, IntPtr, IntPtr*, IntPtr>)&Invoker;
        }

        converted.InvokerMethod = (nint)invoker;
        converted.MethodPointer = (nint)(delegate* unmanaged<IntPtr, Il2CppMethodInfo*, void>)&Method;
        converted.Slot = ushort.MaxValue;
        converted.ReturnType = (Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(Il2CppType.GetClassPointer<Il2CppSystem.Void>());

        converted.Flags = Il2CppMethodFlags.METHOD_ATTRIBUTE_PUBLIC |
                          Il2CppMethodFlags.METHOD_ATTRIBUTE_HIDE_BY_SIG |
                          Il2CppMethodFlags.METHOD_ATTRIBUTE_SPECIAL_NAME |
                          Il2CppMethodFlags.METHOD_ATTRIBUTE_RT_SPECIAL_NAME;

        return converted.MethodInfoPointer;

        [UnmanagedCallersOnly]
        static void Invoker_MetadataV29(IntPtr methodPointer, Il2CppMethodInfo* methodInfo, IntPtr obj, IntPtr* args, IntPtr* returnValue)
        {
            if (returnValue != null)
                *returnValue = IntPtr.Zero;
        }

        [UnmanagedCallersOnly]
        static IntPtr Invoker(IntPtr methodPointer, Il2CppMethodInfo* methodInfo, IntPtr obj, IntPtr* args)
        {
            return IntPtr.Zero;
        }

        [UnmanagedCallersOnly]
        static void Method(IntPtr obj, Il2CppMethodInfo* methodInfo)
        {
        }
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static Il2CppMethodInfo* ConvertMethodInfo(MethodInfo monoMethod, INativeClassStruct declaringClass)
    {
        if (monoMethod.ContainsGenericParameters)
            throw new ArgumentException("Generic methods cannot be converted.", nameof(monoMethod));

        var converted = UnityVersionHandler.NewMethod();
        converted.Name = Marshal.StringToCoTaskMemUTF8(monoMethod.Name);
        converted.Class = declaringClass.ClassPointer;

        var parameters = monoMethod.GetParameters();
        if (parameters.Length > 0)
        {
            converted.ParametersCount = (byte)parameters.Length;
            var paramsArray = UnityVersionHandler.NewMethodParameterArray(parameters.Length);
            converted.Parameters = paramsArray[0];
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameterInfo = parameters[i];
                var param = UnityVersionHandler.Wrap(paramsArray[i]);
                if (param.HasNamePosToken)
                {
                    param.Name = Marshal.StringToCoTaskMemUTF8(parameterInfo.Name);
                    param.Position = i;
                    param.Token = 0;
                }

                param.ParameterType = (Il2CppTypeStruct*)Il2CppTypePointerStore.GetNativeTypePointer(parameterInfo.ParameterType);
            }
        }

        if (!monoMethod.IsAbstract)
        {
            converted.InvokerMethod = Marshal.GetFunctionPointerForDelegate(GetOrCreateInvoker(monoMethod));
            converted.MethodPointer = Marshal.GetFunctionPointerForDelegate(CreateTrampoline(monoMethod, false));
            if (monoMethod.IsVirtual && !monoMethod.IsFinal)
            {
                converted.VirtualMethodPointer = Marshal.GetFunctionPointerForDelegate(CreateTrampoline(monoMethod, true));
            }
            else
            {
                converted.VirtualMethodPointer = converted.MethodPointer; // Not certain if this should be null
            }
        }

        converted.Slot = ushort.MaxValue;

        converted.ReturnType = (Il2CppTypeStruct*)Il2CppTypePointerStore.GetNativeTypePointer(monoMethod.ReturnType);

        converted.Flags = MethodAttributesToMethodFlags(monoMethod.Attributes);

        return converted.MethodInfoPointer;
    }

    [RequiresDynamicCode("")]
    private static Delegate GetOrCreateInvoker(MethodInfo monoMethod)
    {
        return InvokerCache.GetOrAdd(new InvokerSignatureHash(monoMethod),
            static (signatureHash, monoMethodInner) => CreateInvoker(signatureHash, monoMethodInner), monoMethod);
    }

    [RequiresDynamicCode("")]
    private static Delegate CreateInvoker(InvokerSignatureHash signatureHash, MethodInfo monoMethod)
    {
        Debug.Assert(monoMethod.DeclaringType is not null);

        DynamicMethod method;
        if (UnityVersionHandler.IsMetadataV29OrHigher)
        {
            var parameterTypes = new[] { typeof(IntPtr), typeof(Il2CppMethodInfo*), typeof(IntPtr), typeof(IntPtr*), typeof(IntPtr*) };
            // Method pointer
            // Method info pointer
            // this pointer
            // arguments pointer
            // return value pointer (if not void)

            method = new DynamicMethod($"Invoker_{signatureHash}",
                MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(void),
                parameterTypes, typeof(TypeInjector), true);
        }
        else
        {
            var parameterTypes = new[] { typeof(IntPtr), typeof(Il2CppMethodInfo*), typeof(IntPtr), typeof(IntPtr*) };
            // Method pointer
            // Method info pointer
            // this pointer
            // arguments pointer

            method = new DynamicMethod($"Invoker_{signatureHash}",
                MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(IntPtr),
                parameterTypes, typeof(TypeInjector), true);
        }

        var body = method.GetILGenerator();

        if (!monoMethod.IsStatic)
            body.Emit(OpCodes.Ldarg_2); // obj
        for (var i = 0; i < monoMethod.GetParameters().Length; i++)
        {
            var parameterInfo = monoMethod.GetParameters()[i];
            body.Emit(OpCodes.Ldarg_3);
            body.Emit(OpCodes.Ldc_I4, i * IntPtr.Size);
            body.Emit(OpCodes.Add_Ovf_Un);
            var nativeType = TrampolineBuilder.GetNativeType(parameterInfo.ParameterType);
            body.Emit(OpCodes.Ldobj, typeof(IntPtr));
            if (nativeType != typeof(IntPtr))
                body.Emit(OpCodes.Ldobj, nativeType);
        }

        body.Emit(OpCodes.Ldarg_1); // methodMetadata
        body.Emit(OpCodes.Ldarg_0); // methodPointer

        var nativeReturnType = TrampolineBuilder.GetNativeType(monoMethod.ReturnType);
        Type[] nativeParameterTypes =
        [
            ..(ReadOnlySpan<Type>)(monoMethod.IsStatic ? [] : [typeof(IntPtr)]),
            ..monoMethod.GetParameters().Select(it => TrampolineBuilder.GetNativeType(it.ParameterType)),
            typeof(Il2CppMethodInfo*),
        ];
        body.EmitCalli(OpCodes.Calli, CallingConventions.Standard, nativeReturnType, nativeParameterTypes, null);

        if (UnityVersionHandler.IsMetadataV29OrHigher)
        {
            if (monoMethod.ReturnType != typeof(void))
            {
                var returnValue = body.DeclareLocal(nativeReturnType);
                body.Emit(OpCodes.Stloc, returnValue);
                body.Emit(OpCodes.Ldarg_S, (byte)4);
                body.Emit(OpCodes.Ldloc, returnValue);
                body.Emit(OpCodes.Stobj, returnValue.LocalType);
            }
        }
        else
        {
            if (monoMethod.ReturnType == typeof(void))
            {
                // Return null for void methods
                body.Emit(OpCodes.Ldc_I4_0);
                body.Emit(OpCodes.Conv_I);
            }
            else if (monoMethod.ReturnType.IsValueType && nativeReturnType != typeof(IntPtr))
            {
                // If managed return type is a value type and native return type is IntPtr, it means the managed return type is Pointer<> or ByReference<>.
                // Those get returned as-is, so we don't want to box them.
                var returnValue = body.DeclareLocal(nativeReturnType);
                body.Emit(OpCodes.Stloc, returnValue);
                var getClassPointer = typeof(Il2CppType)
                    .GetMethod(nameof(Il2CppType.GetClassPointer), 1, [])!
                    .MakeGenericMethod(monoMethod.ReturnType);
                body.Emit(OpCodes.Call, getClassPointer);
                body.Emit(OpCodes.Ldloca, returnValue);
                body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_value_box))!);
            }
        }

        body.Emit(OpCodes.Ret);

        GCHandle.Alloc(method);

        var invokerDelegateType = UnityVersionHandler.IsMetadataV29OrHigher ? typeof(InvokerDelegateMetadataV29) : typeof(InvokerDelegate);

        var @delegate = method.CreateDelegate(invokerDelegateType);
        GCHandle.Alloc(@delegate);
        return @delegate;
    }

    [RequiresUnreferencedCode("")]
    [RequiresDynamicCode("")]
    private static Delegate CreateTrampoline(MethodInfo monoMethod, bool callVirt)
    {
        var @delegate = TrampolineBuilder.CreateTrampoline(monoMethod, callVirt);
        GCHandle.Alloc(@delegate); // pin it forever
        return @delegate;
    }

    private static (string ImageName, string Namespace, string Name) GetFullyQualifiedName(Type type)
    {
        return (Il2CppType.GetImageName(type), Il2CppType.GetNamespace(type), Il2CppType.GetName(type));
    }

    private static Il2CppClassAttributes TypeAttributesToClassAttributes(TypeAttributes typeAttributes)
    {
        Il2CppClassAttributes result = default;
        result |= (Il2CppClassAttributes)(typeAttributes & TypeAttributes.VisibilityMask);
        result |= (Il2CppClassAttributes)(typeAttributes & TypeAttributes.LayoutMask);
        result |= (Il2CppClassAttributes)(typeAttributes & TypeAttributes.ClassSemanticsMask);
        result |= (Il2CppClassAttributes)(typeAttributes & TypeAttributes.StringFormatMask);
        result |= (typeAttributes & TypeAttributes.Abstract) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_ABSTRACT : default;
        result |= (typeAttributes & TypeAttributes.Sealed) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_SEALED : default;
        result |= (typeAttributes & TypeAttributes.SpecialName) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_SPECIAL_NAME : default;
        result |= (typeAttributes & TypeAttributes.RTSpecialName) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_RT_SPECIAL_NAME : default;
        result |= (typeAttributes & TypeAttributes.Import) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_IMPORT : default;
#pragma warning disable SYSLIB0050 // Type or member is obsolete
        result |= (typeAttributes & TypeAttributes.Serializable) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_SERIALIZABLE : default;
#pragma warning restore SYSLIB0050 // Type or member is obsolete
        result |= (typeAttributes & TypeAttributes.HasSecurity) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_HAS_SECURITY : default;
        result |= (typeAttributes & TypeAttributes.BeforeFieldInit) != 0 ? Il2CppClassAttributes.TYPE_ATTRIBUTE_BEFORE_FIELD_INIT : default;
        return result;
    }

    private static Il2CppMethodFlags MethodAttributesToMethodFlags(MethodAttributes methodAttributes)
    {
        Il2CppMethodFlags result = default;
        result |= (Il2CppMethodFlags)(methodAttributes & MethodAttributes.MemberAccessMask);
        result |= (Il2CppMethodFlags)(methodAttributes & MethodAttributes.VtableLayoutMask);
        result |= (methodAttributes & MethodAttributes.Static) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_STATIC : default;
        result |= (methodAttributes & MethodAttributes.Final) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_FINAL : default;
        result |= (methodAttributes & MethodAttributes.Virtual) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_VIRTUAL : default;
        result |= (methodAttributes & MethodAttributes.HideBySig) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_HIDE_BY_SIG : default;
        result |= (methodAttributes & MethodAttributes.NewSlot) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_NEW_SLOT : default;
        result |= (methodAttributes & MethodAttributes.Abstract) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_ABSTRACT : default;
        result |= (methodAttributes & MethodAttributes.SpecialName) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_SPECIAL_NAME : default;
        result |= (methodAttributes & MethodAttributes.RTSpecialName) != 0 ? Il2CppMethodFlags.METHOD_ATTRIBUTE_RT_SPECIAL_NAME : default;
        return result;
    }

    private static Type GetEnumUnderlyingType([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type enumType)
    {
        // Single instance field
        var instanceField = enumType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
        return instanceField.FieldType;
    }

    [RequiresUnreferencedCode("")]
    private static (string Name, object Value)[] GetEnumNamesAndValues([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type enumType)
    {
        var fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static);
        if (AnyFieldWrongType(enumType, fields))
            fields = fields.Where(f => f.FieldType == enumType).ToArray();
        if (fields.Length == 0)
            return [];

        var instanceField = enumType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single();
        var underlyingType = instanceField.FieldType;
        var primitiveType = underlyingType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).Single().FieldType;
        var underlyingTypeConversionToPrimitiveType = underlyingType.GetMethod("op_Implicit", BindingFlags.Public | BindingFlags.Static, null, [underlyingType], null)!;

        (string Name, object Value)[] result = new (string Name, object Value)[fields.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            var field = fields[i];
            var enumValue = field.GetValue(null);
            var underlyingValue = instanceField.GetValue(enumValue);
            var primitiveValue = underlyingTypeConversionToPrimitiveType.Invoke(null, [underlyingValue])!;
            result[i] = (field.Name, primitiveValue);
        }
        return result;

        static bool AnyFieldWrongType(Type enumType, FieldInfo[] fields)
        {
            foreach (var field in fields)
            {
                if (field.FieldType != enumType)
                    return true;
            }
            return false;
        }
    }

    private static int GetEnumElementSize(Il2CppTypeEnum type)
    {
        return type switch
        {
            Il2CppTypeEnum.IL2CPP_TYPE_I1 => sizeof(sbyte),
            Il2CppTypeEnum.IL2CPP_TYPE_U1 => sizeof(byte),

            Il2CppTypeEnum.IL2CPP_TYPE_CHAR => sizeof(char),

            Il2CppTypeEnum.IL2CPP_TYPE_I2 => sizeof(short),
            Il2CppTypeEnum.IL2CPP_TYPE_U2 => sizeof(ushort),

            Il2CppTypeEnum.IL2CPP_TYPE_I4 => sizeof(int),
            Il2CppTypeEnum.IL2CPP_TYPE_U4 => sizeof(uint),

            Il2CppTypeEnum.IL2CPP_TYPE_I8 => sizeof(long),
            Il2CppTypeEnum.IL2CPP_TYPE_U8 => sizeof(ulong),

            _ => throw new ArgumentOutOfRangeException(nameof(type), type, $"The type provided {type} is invalid")
        };
    }

    private static IntPtr AllocateNewDefaultValueBlob(Il2CppTypeEnum type)
    {
        var size = GetEnumElementSize(type);
        var blob = Marshal.AllocHGlobal(size);
        Logger.Instance.LogTrace("Allocated default value blob at 0x{Blob:X2} of {Size} for {Type}", (long)blob, size, type);
        return blob;
    }

    internal static bool GetFieldDefaultValueOverride(Il2CppFieldInfo* fieldInfo, out IntPtr defaultValueBlob)
    {
        return s_DefaultValueOverrides.TryGetValue((IntPtr)fieldInfo, out defaultValueBlob);
    }

    private static IntPtr CreateOrUpdateFieldDefaultValue(Il2CppFieldInfo* field, Il2CppTypeStruct* type, object value)
    {
        var typeEnum = UnityVersionHandler.Wrap(type).Type;

        if (!GetFieldDefaultValueOverride(field, out var newBlob))
        {
            newBlob = AllocateNewDefaultValueBlob(typeEnum);
            s_DefaultValueOverrides[(IntPtr)field] = newBlob;
        }

        SetFieldDefaultValue(newBlob, typeEnum, value);
        return newBlob;
    }

    private static void SetFieldDefaultValue(IntPtr blob, Il2CppTypeEnum type, object value)
    {
        var valueData = Convert.ToInt64(value);
        switch (type)
        {
            case Il2CppTypeEnum.IL2CPP_TYPE_I1:
                *(sbyte*)blob = (sbyte)valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U1:
                *(byte*)blob = (byte)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
                *(char*)blob = (char)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_I2:
                *(short*)blob = (short)valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U2:
                *(ushort*)blob = (ushort)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_I4:
                *(int*)blob = (int)valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U4:
                *(uint*)blob = (uint)valueData;
                break;

            case Il2CppTypeEnum.IL2CPP_TYPE_I8:
                *(long*)blob = valueData;
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_U8:
                *(ulong*)blob = (ulong)valueData;
                break;

            default: throw new ArgumentOutOfRangeException(nameof(type), type, $"The type provided {type} is invalid");
        }
    }

    private static bool IsStatic(this PropertyInfo property)
    {
        var accessor = property.GetMethod ?? property.SetMethod;
        return accessor is not null && accessor.IsStatic;
    }

    private delegate void InvokerDelegateMetadataV29(IntPtr methodPointer, Il2CppMethodInfo* methodInfo, IntPtr obj, IntPtr* args, IntPtr* returnValue);

    private delegate IntPtr InvokerDelegate(IntPtr methodPointer, Il2CppMethodInfo* methodInfo, IntPtr obj, IntPtr* args);
}
