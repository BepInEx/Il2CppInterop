using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using UnhollowerBaseLib;
using UnhollowerBaseLib.Attributes;
using UnhollowerBaseLib.Runtime;
using UnhollowerBaseLib.Runtime.VersionSpecific.Class;
using UnhollowerBaseLib.Runtime.VersionSpecific.FieldInfo;
using UnhollowerBaseLib.Runtime.VersionSpecific.Type;
using UnhollowerRuntimeLib.Injection;
using UnhollowerRuntimeLib.XrefScans;
using Void = Il2CppSystem.Void;

namespace UnhollowerRuntimeLib
{
    public unsafe class Il2CppInterfaceCollection : List<INativeClassStruct>
    {
        public Il2CppInterfaceCollection(IEnumerable<INativeClassStruct> interfaces) : base(interfaces) { }

        public Il2CppInterfaceCollection(IEnumerable<Type> interfaces) : base(ResolveNativeInterfaces(interfaces)) { }

        private static IEnumerable<INativeClassStruct> ResolveNativeInterfaces(IEnumerable<Type> interfaces)
        {
            return interfaces.Select(it =>
            {
                var classPointer = Il2CppClassPointerStore.GetNativeClassPointer(it);
                if (classPointer == IntPtr.Zero)
                    throw new ArgumentException(
                        $"Type {it} doesn't have an IL2CPP class pointer, which means it's not an IL2CPP interface");
                return UnityVersionHandler.Wrap((Il2CppClass*)classPointer);
            });
        }

        public static implicit operator Il2CppInterfaceCollection(INativeClassStruct[] interfaces) => new(interfaces);

        public static implicit operator Il2CppInterfaceCollection(Type[] interfaces) => new(interfaces);
    }

    public class RegisterTypeOptions
    {
        public static readonly RegisterTypeOptions Default = new();

        public bool LogSuccess { get; init; } = true;
        public Func<Type, Type[]> InterfacesResolver { get; init; } = null;
        public Il2CppInterfaceCollection Interfaces { get; init; } = null;
    }

    public unsafe static class ClassInjector
    {

        /// <summary> type.FullName </summary>
        private static readonly HashSet<string> InjectedTypes = new HashSet<string>();
        /// <summary> (method) : (method_inst, method) </summary>
        private static readonly Dictionary<IntPtr, (MethodInfo, Dictionary<IntPtr, IntPtr>)> InflatedMethodFromContextDictionary = new Dictionary<IntPtr, (MethodInfo, Dictionary<IntPtr, IntPtr>)>();

        public static void ProcessNewObject(Il2CppObjectBase obj)
        {
            var pointer = obj.Pointer;
            var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
            AssignGcHandle(pointer, handle);
        }

        public static IntPtr DerivedConstructorPointer<T>()
        {
            return IL2CPP.il2cpp_object_new(Il2CppClassPointerStore<T>.NativeClassPtr); // todo: consider calling base constructor
        }

        public static void DerivedConstructorBody(Il2CppObjectBase objectBase)
        {
            if (objectBase.isWrapped)
                return;
            FieldInfo[] fields = objectBase.GetType()
                 .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                 .Where(IsFieldEligible)
                 .ToArray();
            foreach (FieldInfo field in fields)
            {
                field.SetValue(objectBase, field.FieldType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new Type[] { typeof(Il2CppObjectBase), typeof(string) }, Array.Empty<ParameterModifier>())
                    .Invoke(new object[] { objectBase, field.Name })
                );
            }
            var ownGcHandle = GCHandle.Alloc(objectBase, GCHandleType.Normal);
            AssignGcHandle(objectBase.Pointer, ownGcHandle);
        }

        public static void AssignGcHandle(IntPtr pointer, GCHandle gcHandle)
        {
            var handleAsPointer = GCHandle.ToIntPtr(gcHandle);
            if (pointer == IntPtr.Zero) throw new NullReferenceException(nameof(pointer));
            ClassInjectorBase.GetInjectedData(pointer)->managedGcHandle = GCHandle.ToIntPtr(gcHandle);
        }


        public static bool IsTypeRegisteredInIl2Cpp<T>() where T : class => IsTypeRegisteredInIl2Cpp(typeof(T));
        public static bool IsTypeRegisteredInIl2Cpp(Type type)
        {
            var currentPointer = Il2CppClassPointerStore.GetNativeClassPointer(type);
            if (currentPointer != IntPtr.Zero)
                return true;
            lock (InjectedTypes)
                if (InjectedTypes.Contains(type.FullName))
                    return true;
            return false;
        }

        [Obsolete("Use RegisterTypeInIl2Cpp<T>(RegisterTypeOptions)", true)]
        public static void RegisterTypeInIl2Cpp<T>(bool logSuccess) where T : class => RegisterTypeInIl2Cpp(typeof(T), new RegisterTypeOptions { LogSuccess = logSuccess });
        [Obsolete("Use RegisterTypeInIl2Cpp(Type, RegisterTypeOptions)", true)]
        public static void RegisterTypeInIl2Cpp(Type type, bool logSuccess) => RegisterTypeInIl2Cpp(type, new RegisterTypeOptions { LogSuccess = logSuccess });
        [Obsolete("Use RegisterTypeInIl2Cpp(Type, RegisterTypeOptions) or [Il2CppImplementsAttribute]", true)]
        public static void RegisterTypeInIl2CppWithInterfaces<T>(params Type[] interfaces) where T : class => RegisterTypeInIl2CppWithInterfaces(typeof(T), true, interfaces);
        [Obsolete("Use RegisterTypeInIl2Cpp(Type, RegisterTypeOptions) or [Il2CppImplementsAttribute]", true)]
        public static void RegisterTypeInIl2CppWithInterfaces<T>(bool logSuccess, params Type[] interfaces) where T : class => RegisterTypeInIl2CppWithInterfaces(typeof(T), logSuccess, interfaces);
        [Obsolete("Use RegisterTypeInIl2Cpp(Type, RegisterTypeOptions) or [Il2CppImplementsAttribute]", true)]
        public static void RegisterTypeInIl2CppWithInterfaces(Type type, bool logSuccess, params Type[] interfaces) => RegisterTypeInIl2Cpp(type, new RegisterTypeOptions() { LogSuccess = logSuccess, Interfaces = interfaces });
        [Obsolete("Use RegisterTypeInIl2Cpp(Type, RegisterTypeOptions)", true)]
        public static void RegisterTypeInIl2Cpp(Type type, bool logSuccess, params INativeClassStruct[] interfaces) => RegisterTypeInIl2Cpp(type, new RegisterTypeOptions { LogSuccess = logSuccess, Interfaces = interfaces });

        public static void RegisterTypeInIl2Cpp<T>() where T : class => RegisterTypeInIl2Cpp(typeof(T));
        public static void RegisterTypeInIl2Cpp(Type type) => RegisterTypeInIl2Cpp(type, RegisterTypeOptions.Default);
        public static void RegisterTypeInIl2Cpp<T>(RegisterTypeOptions options) where T : class => RegisterTypeInIl2Cpp(typeof(T), options);

        public static void RegisterTypeInIl2Cpp(Type type, RegisterTypeOptions options)
        {
            var interfaces = options.Interfaces;
            if (interfaces == null)
            {
                var interfacesAttribute = type.GetCustomAttribute<Il2CppImplementsAttribute>();
                interfaces = interfacesAttribute?.Interfaces ?? options.InterfacesResolver?.Invoke(type) ?? Array.Empty<Type>();
            }

            if (type == null)
                throw new ArgumentException($"Type argument cannot be null");

            if (type.IsGenericType || type.IsGenericTypeDefinition)
                throw new ArgumentException($"Type {type} is generic and can't be used in il2cpp");

            var currentPointer = Il2CppClassPointerStore.GetNativeClassPointer(type);
            if (currentPointer != IntPtr.Zero)
                return; //already registered in il2cpp

            var baseType = type.BaseType;
            if (baseType == null)
                throw new ArgumentException($"Class {type} does not inherit from a class registered in il2cpp");

            var baseClassPointer = UnityVersionHandler.Wrap((Il2CppClass*)Il2CppClassPointerStore.GetNativeClassPointer(baseType));
            if (baseClassPointer == null)
            {
                RegisterTypeInIl2Cpp(baseType, new RegisterTypeOptions() { LogSuccess = options.LogSuccess });
                baseClassPointer = UnityVersionHandler.Wrap((Il2CppClass*)Il2CppClassPointerStore.GetNativeClassPointer(baseType));
            }

            InjectorHelpers.Setup();

            // Initialize the vtable of all base types (Class::Init is recursive internally)
            InjectorHelpers.ClassInit(baseClassPointer.ClassPointer);

            if (baseClassPointer.ValueType || baseClassPointer.EnumType)
                throw new ArgumentException($"Base class {baseType} is value type and can't be inherited from");

            if (baseClassPointer.IsGeneric)
                throw new ArgumentException($"Base class {baseType} is generic and can't be inherited from");

            if ((baseClassPointer.Flags & Il2CppClassAttributes.TYPE_ATTRIBUTE_SEALED) != 0)
                throw new ArgumentException($"Base class {baseType} is sealed and can't be inherited from");

            if ((baseClassPointer.Flags & Il2CppClassAttributes.TYPE_ATTRIBUTE_INTERFACE) != 0)
                throw new ArgumentException($"Base class {baseType} is an interface and can't be inherited from");

            if (interfaces.Any(i => (i.Flags & Il2CppClassAttributes.TYPE_ATTRIBUTE_INTERFACE) == 0))
                throw new ArgumentException($"Some of the interfaces in {interfaces} are not interfaces");

            lock (InjectedTypes)
                if (!InjectedTypes.Add(type.FullName))
                    throw new ArgumentException($"Type with FullName {type.FullName} is already injected. Don't inject the same type twice, or use a different namespace");

            if (ourOriginalGenericGetMethod == null) HookGenericMethodGetMethod();

            var interfaceFunctionCount = interfaces.Sum(i => i.MethodCount);
            var classPointer = UnityVersionHandler.NewClass(baseClassPointer.VtableCount + interfaceFunctionCount);

            classPointer.Image = InjectorHelpers.InjectedImage.ImagePointer;
            classPointer.Parent = baseClassPointer.ClassPointer;
            classPointer.ElementClass = classPointer.Class = classPointer.CastClass = classPointer.ClassPointer;
            classPointer.NativeSize = -1;
            classPointer.ActualSize = classPointer.InstanceSize = baseClassPointer.InstanceSize;

            classPointer.Initialized = true;
            classPointer.InitializedAndNoError = true;
            classPointer.SizeInited = true;
            classPointer.HasFinalize = true;
            classPointer.IsVtableInitialized = true;

            classPointer.Name = Marshal.StringToHGlobalAnsi(type.Name);
            classPointer.Namespace = Marshal.StringToHGlobalAnsi(type.Namespace ?? string.Empty);

            classPointer.ThisArg.Type = classPointer.ByValArg.Type = Il2CppTypeEnum.IL2CPP_TYPE_CLASS;
            classPointer.ThisArg.ByRef = true;

            classPointer.Flags = baseClassPointer.Flags; // todo: adjust flags?

            FieldInfo[] fieldsToInject = type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(IsFieldEligible)
                .ToArray();
            classPointer.FieldCount = (ushort)fieldsToInject.Length;

            Il2CppFieldInfo* il2cppFields = (Il2CppFieldInfo*)Marshal.AllocHGlobal(classPointer.FieldCount * UnityVersionHandler.FieldInfoSize());
            int fieldOffset = (int)classPointer.InstanceSize;
            for (int i = 0; i < classPointer.FieldCount; i++)
            {
                INativeFieldInfoStruct fieldInfo = UnityVersionHandler.Wrap(il2cppFields + (i * UnityVersionHandler.FieldInfoSize()));
                fieldInfo.Name = Marshal.StringToHGlobalAnsi(fieldsToInject[i].Name);
                fieldInfo.Parent = classPointer.ClassPointer;
                fieldInfo.Offset = fieldOffset;

                Type fieldType = fieldsToInject[i].FieldType.GenericTypeArguments[0];
                FieldAttributes fieldAttributes = fieldsToInject[i].Attributes;
                IntPtr fieldInfoClass = Il2CppClassPointerStore.GetNativeClassPointer(fieldType);
                if (!_injectedFieldTypes.TryGetValue((fieldType, fieldAttributes), out IntPtr fieldTypePtr))
                {
                    INativeTypeStruct classType = UnityVersionHandler.Wrap((Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(fieldInfoClass));

                    INativeTypeStruct duplicatedType = UnityVersionHandler.NewType();
                    duplicatedType.Data = classType.Data;
                    duplicatedType.Attrs = (ushort)fieldAttributes;
                    duplicatedType.Type = classType.Type;
                    duplicatedType.ByRef = classType.ByRef;
                    duplicatedType.Pinned = classType.Pinned;

                    _injectedFieldTypes[(fieldType, fieldAttributes)] = duplicatedType.Pointer;
                    fieldTypePtr = duplicatedType.Pointer;
                }

                fieldInfo.Type = (Il2CppTypeStruct*)fieldTypePtr;
                if (fieldInfoClass == IntPtr.Zero)
                    throw new Exception($"Type {fieldType} in {type}.{fieldsToInject[i].Name} doesn't exist in Il2Cpp");

                if (IL2CPP.il2cpp_class_is_valuetype(fieldInfoClass))
                {
                    uint _align = 0;
                    int fieldSize = IL2CPP.il2cpp_class_value_size(fieldInfoClass, ref _align);
                    fieldOffset += fieldSize;
                }
                else
                {
                    fieldOffset += sizeof(Il2CppObject*);
                }
            }
            classPointer.Fields = il2cppFields;

            classPointer.InstanceSize = (uint)(fieldOffset + sizeof(InjectedClassData));
            classPointer.ActualSize = classPointer.InstanceSize;

            var eligibleMethods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly).Where(IsMethodEligible).ToArray();
            var methodCount = 2 + eligibleMethods.Length; // 1 is the finalizer, 1 is empty ctor

            classPointer.MethodCount = (ushort)methodCount;
            var methodPointerArray = (Il2CppMethodInfo**)Marshal.AllocHGlobal(methodCount * IntPtr.Size);
            classPointer.Methods = methodPointerArray;

            methodPointerArray[0] = ConvertStaticMethod(FinalizeDelegate, "Finalize", classPointer);
            var finalizeMethod = UnityVersionHandler.Wrap(methodPointerArray[0]);
            if (!type.IsAbstract) methodPointerArray[1] = ConvertStaticMethod(CreateEmptyCtor(type, fieldsToInject), ".ctor", classPointer);
            Dictionary<(string name, int paramCount, bool isGeneric), int> infos = new Dictionary<(string, int, bool), int>(eligibleMethods.Length);
            for (var i = 0; i < eligibleMethods.Length; i++)
            {
                var methodInfo = eligibleMethods[i];
                var methodInfoPointer = methodPointerArray[i + 2] = ConvertMethodInfo(methodInfo, classPointer);
                if (methodInfo.IsGenericMethod)
                    InflatedMethodFromContextDictionary.Add((IntPtr)methodInfoPointer, (methodInfo, new Dictionary<IntPtr, IntPtr>()));
                infos[(methodInfo.Name, methodInfo.GetParameters().Length, methodInfo.IsGenericMethod)] = i + 2;
            }

            var vTablePointer = (VirtualInvokeData*)classPointer.VTable;
            var baseVTablePointer = (VirtualInvokeData*)baseClassPointer.VTable;
            classPointer.VtableCount = (ushort)(baseClassPointer.VtableCount + interfaceFunctionCount);

            //Abstract and Virtual Fix
            if (classPointer.Flags.HasFlag(Il2CppClassAttributes.TYPE_ATTRIBUTE_ABSTRACT) && IL2CPP.il2cpp_class_is_abstract((IntPtr)baseClassPointer.Class))
            {
                //Inheriting from an abstract class, make injected class not abstract.
                classPointer.Flags &= ~Il2CppClassAttributes.TYPE_ATTRIBUTE_ABSTRACT;

                int nativeMethodCount = baseClassPointer.MethodCount;
                List<int> methofPointerArrayIndices = new List<int>();
                for (int x = 0; x < nativeMethodCount; x++)
                {
                    var method = UnityVersionHandler.Wrap(baseClassPointer.Methods[x]);

                    //VTable entries for abstact methods are empty point them to implementation methods
                    if (method.Flags.HasFlag(Il2CppMethodFlags.METHOD_ATTRIBUTE_ABSTRACT))
                    {
                        var name = Marshal.PtrToStringAnsi(method.Name);
                        var parameters = new Type[method.ParametersCount];

                        for (var i = 0; i < method.ParametersCount; i++)
                        {
                            var parameterInfo = UnityVersionHandler.Wrap(method.Parameters, i);
                            var parameterType = SystemTypeFromIl2CppType(parameterInfo.ParameterType);

                            parameters[i] = parameterType;
                        }

                        var monoMethodImplementation = type.GetMethod(name, parameters);

                        var methodPointerArrayIndex = Array.IndexOf(eligibleMethods, monoMethodImplementation);
                        if (methodPointerArrayIndex < 0)
                        {
                            throw new ArgumentException($"{type.Name} does not implement the abstract method {name}");
                        }
                        else
                        {
                            methodPointerArrayIndex += 2;
                            methofPointerArrayIndices.Add(methodPointerArrayIndex);
                        }
                    }
                }


                int abstractMethodIndex = 0;
                int[] abstractIndices = methofPointerArrayIndices.ToArray();

                for (var y = 0; y < classPointer.VtableCount; y++)
                {
                    if ((int)baseVTablePointer[y].methodPtr == 0)
                    {
                        var method = UnityVersionHandler.Wrap(methodPointerArray[abstractIndices[abstractMethodIndex]]);
                        vTablePointer[y].method = methodPointerArray[abstractIndices[abstractMethodIndex]];
                        vTablePointer[y].methodPtr = method.MethodPointer;
                        abstractMethodIndex++;

                    }
                }
            }

            for (var i = 0; i < baseClassPointer.VtableCount; i++)
            {
                if (baseVTablePointer[i].methodPtr == IntPtr.Zero) continue;
                vTablePointer[i] = baseVTablePointer[i];
                string Il2CppMethodName = Marshal.PtrToStringAnsi(UnityVersionHandler.Wrap(vTablePointer[i].method).Name);
                MethodInfo monoMethodImplementation = type.GetMethod(Il2CppMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

                int methodPointerArrayIndex = Array.IndexOf(eligibleMethods, monoMethodImplementation);
                if (methodPointerArrayIndex > 0)
                {
                    var method = UnityVersionHandler.Wrap(methodPointerArray[methodPointerArrayIndex + 2]);
                    vTablePointer[i].method = methodPointerArray[methodPointerArrayIndex + 2];
                    vTablePointer[i].methodPtr = method.MethodPointer;
                }

                if (Il2CppMethodName == "Finalize") // slot number is not static
                {
                    vTablePointer[i].method = methodPointerArray[0];
                    vTablePointer[i].methodPtr = finalizeMethod.MethodPointer;
                }
            }

            var offsets = new int[interfaces.Count];

            var index = baseClassPointer.VtableCount;
            for (var i = 0; i < interfaces.Count; i++)
            {
                offsets[i] = index;
                for (var j = 0; j < interfaces[i].MethodCount; j++)
                {
                    var vTableMethod = UnityVersionHandler.Wrap(interfaces[i].Methods[j]);
                    var methodName = Marshal.PtrToStringAnsi(vTableMethod.Name);
                    if (!infos.TryGetValue((methodName, vTableMethod.ParametersCount, vTableMethod.IsGeneric), out var methodIndex))
                    {
                        ++index;
                        continue;
                    }
                    var method = methodPointerArray[methodIndex];
                    vTablePointer[index].method = method;
                    vTablePointer[index].methodPtr = UnityVersionHandler.Wrap(method).MethodPointer;
                    ++index;
                }
            }

            var interfaceCount = baseClassPointer.InterfaceCount + interfaces.Count;
            classPointer.InterfaceCount = (ushort)interfaceCount;
            classPointer.ImplementedInterfaces = (Il2CppClass**)Marshal.AllocHGlobal(interfaceCount * IntPtr.Size);
            for (int i = 0; i < baseClassPointer.InterfaceCount; i++)
                classPointer.ImplementedInterfaces[i] = baseClassPointer.ImplementedInterfaces[i];
            for (int i = baseClassPointer.InterfaceCount; i < interfaceCount; i++)
                classPointer.ImplementedInterfaces[i] = interfaces[i - baseClassPointer.InterfaceCount].ClassPointer;

            var interfaceOffsetsCount = baseClassPointer.InterfaceOffsetsCount + interfaces.Count;
            classPointer.InterfaceOffsetsCount = (ushort)interfaceOffsetsCount;
            classPointer.InterfaceOffsets = (Il2CppRuntimeInterfaceOffsetPair*)Marshal.AllocHGlobal(interfaceOffsetsCount * Marshal.SizeOf<Il2CppRuntimeInterfaceOffsetPair>());
            for (int i = 0; i < baseClassPointer.InterfaceOffsetsCount; i++)
                classPointer.InterfaceOffsets[i] = baseClassPointer.InterfaceOffsets[i];
            for (int i = baseClassPointer.InterfaceOffsetsCount; i < interfaceOffsetsCount; i++)
                classPointer.InterfaceOffsets[i] = new Il2CppRuntimeInterfaceOffsetPair
                {
                    interfaceType = interfaces[i - baseClassPointer.InterfaceOffsetsCount].ClassPointer,
                    offset = offsets[i - baseClassPointer.InterfaceOffsetsCount]
                };

            var TypeHierarchyDepth = 1 + baseClassPointer.TypeHierarchyDepth;
            classPointer.TypeHierarchyDepth = (byte)TypeHierarchyDepth;
            classPointer.TypeHierarchy = (Il2CppClass**)Marshal.AllocHGlobal(TypeHierarchyDepth * IntPtr.Size);
            for (var i = 0; i < TypeHierarchyDepth; i++)
                classPointer.TypeHierarchy[i] = baseClassPointer.TypeHierarchy[i];
            classPointer.TypeHierarchy[TypeHierarchyDepth - 1] = classPointer.ClassPointer;

            classPointer.ByValArg.Data = classPointer.ThisArg.Data = (IntPtr)InjectorHelpers.CreateClassToken(classPointer.Pointer);

            RuntimeSpecificsStore.SetClassInfo(classPointer.Pointer, true, true);
            Il2CppClassPointerStore.SetNativeClassPointer(type, classPointer.Pointer);

            InjectorHelpers.AddTypeToLookup(type, classPointer.Pointer);

            if (options.LogSuccess) LogSupport.Info($"Registered mono type {type} in il2cpp domain");
        }

        private static bool IsTypeSupported(Type type)
        {
            if (type.IsValueType ||
                type == typeof(string) ||
                type.IsGenericParameter) return true;
            if (type.IsByRef) return IsTypeSupported(type.GetElementType());

            return typeof(Il2CppObjectBase).IsAssignableFrom(type);
        }

        private static bool IsFieldEligible(FieldInfo field)
        {
            if (!field.FieldType.IsGenericType) return false;
            Type genericTypeDef = field.FieldType.GetGenericTypeDefinition();
            if (genericTypeDef != typeof(Il2CppReferenceField<>) && genericTypeDef != typeof(Il2CppValueField<>)) return false;

            return IsTypeSupported(field.FieldType.GenericTypeArguments[0]);
        }

        private static bool IsMethodEligible(MethodInfo method)
        {
            if (method.Name == "Finalize") return false;
            if (method.IsStatic || method.IsAbstract) return false;
            if (method.CustomAttributes.Any(it => typeof(HideFromIl2CppAttribute).IsAssignableFrom(it.AttributeType))) return false;

            if (method.DeclaringType != null)
            {
                if (method.DeclaringType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                        .Where(property => property.GetAccessors(true).Contains(method))
                        .Any(property => property.CustomAttributes.Any(it => typeof(HideFromIl2CppAttribute).IsAssignableFrom(it.AttributeType)))
                )
                {
                    return false;
                }

                foreach (var eventInfo in method.DeclaringType.GetEvents(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    if ((eventInfo.GetAddMethod(true) == method || eventInfo.GetRemoveMethod(true) == method) && eventInfo.GetCustomAttribute<HideFromIl2CppAttribute>() != null)
                    {
                        return false;
                    }
                }
            }

            if (!IsTypeSupported(method.ReturnType))
            {
                LogSupport.Warning($"Method {method} on type {method.DeclaringType} has unsupported return type {method.ReturnType}");
                return false;
            }

            foreach (var parameter in method.GetParameters())
            {
                var parameterType = parameter.ParameterType;
                if (!IsTypeSupported(parameterType))
                {
                    LogSupport.Warning($"Method {method} on type {method.DeclaringType} has unsupported parameter {parameter} of type {parameterType}");
                    return false;
                }
            }

            return true;
        }

        private static Il2CppMethodInfo* ConvertStaticMethod(VoidCtorDelegate voidCtor, string methodName, INativeClassStruct declaringClass)
        {
            var converted = UnityVersionHandler.NewMethod();
            converted.Name = Marshal.StringToHGlobalAnsi(methodName);
            converted.Class = declaringClass.ClassPointer;

            converted.InvokerMethod = Marshal.GetFunctionPointerForDelegate(new InvokerDelegate(StaticVoidIntPtrInvoker));
            converted.MethodPointer = Marshal.GetFunctionPointerForDelegate(voidCtor);
            converted.Slot = ushort.MaxValue;
            converted.ReturnType = (Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore<Void>.NativeClassPtr);

            converted.Flags = Il2CppMethodFlags.METHOD_ATTRIBUTE_PUBLIC |
                               Il2CppMethodFlags.METHOD_ATTRIBUTE_HIDE_BY_SIG | Il2CppMethodFlags.METHOD_ATTRIBUTE_SPECIAL_NAME | Il2CppMethodFlags.METHOD_ATTRIBUTE_RT_SPECIAL_NAME;

            return converted.MethodInfoPointer;
        }

        private static Il2CppMethodInfo* ConvertMethodInfo(MethodInfo monoMethod, INativeClassStruct declaringClass)
        {
            var converted = UnityVersionHandler.NewMethod();
            converted.Name = Marshal.StringToHGlobalAnsi(monoMethod.Name);
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
                    if (UnityVersionHandler.ParameterInfoHasNamePosToken())
                    {
                        param.Name = Marshal.StringToHGlobalAnsi(parameterInfo.Name);
                        param.Position = i;
                        param.Token = 0;
                    }
                    var parameterType = parameterInfo.ParameterType;
                    if (!parameterType.IsGenericParameter)
                    {
                        if (parameterType.IsByRef)
                        {
                            Type elementType = parameterType.GetElementType();
                            if (!elementType.IsGenericParameter)
                            {
                                var elemType = UnityVersionHandler.Wrap((Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore.GetNativeClassPointer(elementType)));
                                var refType = UnityVersionHandler.NewType();
                                refType.Data = elemType.Data;
                                refType.Attrs = elemType.Attrs;
                                refType.Type = elemType.Type;
                                refType.ByRef = true;
                                refType.Pinned = elemType.Pinned;
                                param.ParameterType = refType.TypePointer;
                            }
                            else
                            {
                                var type = UnityVersionHandler.NewType();
                                type.Type = Il2CppTypeEnum.IL2CPP_TYPE_MVAR;
                                type.ByRef = true;
                                param.ParameterType = type.TypePointer;
                            }
                        }
                        else
                            param.ParameterType = (Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore.GetNativeClassPointer(parameterType));
                    }
                    else
                    {
                        var type = UnityVersionHandler.NewType();
                        type.Type = Il2CppTypeEnum.IL2CPP_TYPE_MVAR;
                        param.ParameterType = type.TypePointer;
                    }
                }
            }

            if (monoMethod.IsGenericMethod)
            {
                if (monoMethod.ContainsGenericParameters)
                    converted.IsGeneric = true;
                else
                    converted.IsInflated = true;
            }

            if (!monoMethod.ContainsGenericParameters)
            {
                converted.InvokerMethod = Marshal.GetFunctionPointerForDelegate(GetOrCreateInvoker(monoMethod));
                converted.MethodPointer = Marshal.GetFunctionPointerForDelegate(GetOrCreateTrampoline(monoMethod));
            }
            converted.Slot = ushort.MaxValue;

            if (!monoMethod.ReturnType.IsGenericParameter)
                converted.ReturnType = (Il2CppTypeStruct*)IL2CPP.il2cpp_class_get_type(Il2CppClassPointerStore.GetNativeClassPointer(monoMethod.ReturnType));
            else
            {
                var type = UnityVersionHandler.NewType();
                type.Type = Il2CppTypeEnum.IL2CPP_TYPE_MVAR;
                converted.ReturnType = type.TypePointer;
            }

            converted.Flags = Il2CppMethodFlags.METHOD_ATTRIBUTE_PUBLIC |
                               Il2CppMethodFlags.METHOD_ATTRIBUTE_HIDE_BY_SIG;

            return converted.MethodInfoPointer;
        }

        private static VoidCtorDelegate CreateEmptyCtor(Type targetType, FieldInfo[] fieldsToInitialize)
        {
            var method = new DynamicMethod("FromIl2CppCtorDelegate", MethodAttributes.Public | MethodAttributes.Static, CallingConventions.Standard, typeof(void), new[] { typeof(IntPtr) }, targetType, true);

            var body = method.GetILGenerator();

            var monoCtor = targetType.GetConstructor(new[] { typeof(IntPtr) });
            if (monoCtor != null)
            {
                body.Emit(OpCodes.Ldarg_0);
                body.Emit(OpCodes.Newobj, monoCtor);
            }
            else
            {
                var local = body.DeclareLocal(targetType);
                body.Emit(OpCodes.Ldtoken, targetType);
                body.Emit(OpCodes.Call, typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle), BindingFlags.Public | BindingFlags.Static)!);
                body.Emit(OpCodes.Call, typeof(FormatterServices).GetMethod(nameof(FormatterServices.GetUninitializedObject), BindingFlags.Public | BindingFlags.Static)!);
                body.Emit(OpCodes.Stloc, local);
                body.Emit(OpCodes.Ldloc, local);
                body.Emit(OpCodes.Ldarg_0);
                body.Emit(OpCodes.Call, typeof(Il2CppObjectBase).GetMethod(nameof(Il2CppObjectBase.CreateGCHandle), BindingFlags.NonPublic | BindingFlags.Instance)!);
                body.Emit(OpCodes.Ldloc, local);
                body.Emit(OpCodes.Ldc_I4_1);
                body.Emit(OpCodes.Stfld, typeof(Il2CppObjectBase).GetField(nameof(Il2CppObjectBase.isWrapped), BindingFlags.NonPublic | BindingFlags.Instance)!);
                body.Emit(OpCodes.Ldloc, local);
                body.Emit(OpCodes.Call, targetType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, Array.Empty<ParameterModifier>())!);
                body.Emit(OpCodes.Ldloc, local);
            }
            foreach (FieldInfo field in fieldsToInitialize)
            {
                body.Emit(OpCodes.Dup);
                body.Emit(OpCodes.Dup);
                body.Emit(OpCodes.Ldstr, field.Name);
                body.Emit(OpCodes.Newobj, field.FieldType.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                    new Type[] { typeof(Il2CppObjectBase), typeof(string) }, Array.Empty<ParameterModifier>())
                );
                body.Emit(OpCodes.Stfld, field);
            }
            body.Emit(OpCodes.Call, typeof(ClassInjector).GetMethod(nameof(ProcessNewObject))!);

            body.Emit(OpCodes.Ret);

            var @delegate = (VoidCtorDelegate)method.CreateDelegate(typeof(VoidCtorDelegate));
            GCHandle.Alloc(@delegate); // pin it forever
            return @delegate;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr InvokerDelegate(IntPtr methodPointer, Il2CppMethodInfo* methodInfo, IntPtr obj, IntPtr* args);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void VoidCtorDelegate(IntPtr objectPointer);

        public static void Finalize(IntPtr ptr)
        {
            var gcHandle = ClassInjectorBase.GetGcHandlePtrFromIl2CppObject(ptr);
            GCHandle.FromIntPtr(gcHandle).Free();
        }

        private static readonly ConcurrentDictionary<string, InvokerDelegate> InvokerCache = new ConcurrentDictionary<string, InvokerDelegate>();

        private static InvokerDelegate GetOrCreateInvoker(MethodInfo monoMethod)
        {
            return InvokerCache.GetOrAdd(ExtractSignature(monoMethod), (_, monoMethodInner) => CreateInvoker(monoMethodInner), monoMethod);
        }

        private static Delegate GetOrCreateTrampoline(MethodInfo monoMethod)
        {
            return CreateTrampoline(monoMethod);
        }

        private static InvokerDelegate CreateInvoker(MethodInfo monoMethod)
        {
            var parameterTypes = new[] { typeof(IntPtr), typeof(Il2CppMethodInfo*), typeof(IntPtr), typeof(IntPtr*) };

            var method = new DynamicMethod("Invoker_" + ExtractSignature(monoMethod), MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard, typeof(IntPtr), parameterTypes, monoMethod.DeclaringType, true);

            var body = method.GetILGenerator();

            body.Emit(OpCodes.Ldarg_2);
            for (var i = 0; i < monoMethod.GetParameters().Length; i++)
            {
                var parameterInfo = monoMethod.GetParameters()[i];
                body.Emit(OpCodes.Ldarg_3);
                body.Emit(OpCodes.Ldc_I4, i * IntPtr.Size);
                body.Emit(OpCodes.Add_Ovf_Un);
                var nativeType = parameterInfo.ParameterType.NativeType();
                body.Emit(OpCodes.Ldobj, typeof(IntPtr));
                if (nativeType != typeof(IntPtr))
                    body.Emit(OpCodes.Ldobj, nativeType);
            }

            body.Emit(OpCodes.Ldarg_0);
            body.EmitCalli(OpCodes.Calli, CallingConvention.Cdecl, monoMethod.ReturnType.NativeType(), new[] { typeof(IntPtr) }.Concat(monoMethod.GetParameters().Select(it => it.ParameterType.NativeType())).ToArray());

            if (monoMethod.ReturnType == typeof(void))
            {
                body.Emit(OpCodes.Ldc_I4_0);
                body.Emit(OpCodes.Conv_I);
            }
            else if (monoMethod.ReturnType.IsValueType)
            {
                var returnValue = body.DeclareLocal(monoMethod.ReturnType);
                body.Emit(OpCodes.Stloc, returnValue);
                var classField = typeof(Il2CppClassPointerStore<>).MakeGenericType(monoMethod.ReturnType)
                                                                  .GetField(nameof(Il2CppClassPointerStore<int>.NativeClassPtr));
                body.Emit(OpCodes.Ldsfld, classField);
                body.Emit(OpCodes.Ldloca, returnValue);
                body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_value_box))!);
            }

            body.Emit(OpCodes.Ret);

            return (InvokerDelegate)method.CreateDelegate(typeof(InvokerDelegate));
        }

        private static IntPtr StaticVoidIntPtrInvoker(IntPtr methodPointer, Il2CppMethodInfo* methodInfo, IntPtr obj, IntPtr* args)
        {
            Marshal.GetDelegateForFunctionPointer<VoidCtorDelegate>(methodPointer)(obj);
            return IntPtr.Zero;
        }

        private static Delegate CreateTrampoline(MethodInfo monoMethod)
        {
            var nativeParameterTypes = new[] { typeof(IntPtr) }.Concat(monoMethod.GetParameters()
                .Select(it => it.ParameterType.NativeType()).Concat(new[] { typeof(Il2CppMethodInfo*) })).ToArray();

            var managedParameters = new[] { monoMethod.DeclaringType }.Concat(monoMethod.GetParameters().Select(it => it.ParameterType)).ToArray();

            var method = new DynamicMethod("Trampoline_" + ExtractSignature(monoMethod) + monoMethod.DeclaringType + monoMethod.Name,
                MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard,
                monoMethod.ReturnType.NativeType(), nativeParameterTypes,
                monoMethod.DeclaringType, true);

            var signature = new DelegateSupport.MethodSignature(monoMethod, true);
            var delegateType = DelegateSupport.GetOrCreateDelegateType(signature, monoMethod);

            var body = method.GetILGenerator();

            body.BeginExceptionBlock();

            body.Emit(OpCodes.Ldarg_0);
            body.Emit(OpCodes.Call, typeof(ClassInjectorBase).GetMethod(nameof(ClassInjectorBase.GetMonoObjectFromIl2CppPointer))!);
            body.Emit(OpCodes.Castclass, monoMethod.DeclaringType);

            var indirectVariables = new LocalBuilder[managedParameters.Length];

            for (var i = 1; i < managedParameters.Length; i++)
            {
                var parameter = managedParameters[i];
                if (parameter.IsSubclassOf(typeof(Il2CppSystem.ValueType)))
                {
                    body.Emit(OpCodes.Ldc_I8, Il2CppClassPointerStore.GetNativeClassPointer(parameter).ToInt64());
                    body.Emit(OpCodes.Conv_I);
                    body.Emit(Environment.Is64BitProcess ? OpCodes.Ldarg : OpCodes.Ldarga_S, i);
                    body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.il2cpp_value_box)));
                }
                else body.Emit(OpCodes.Ldarg, i);
                if (parameter.IsValueType) continue;

                void HandleTypeConversion(Type type)
                {
                    if (type == typeof(string))
                        body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.Il2CppStringToManaged))!);
                    else if (type.IsSubclassOf(typeof(Il2CppObjectBase)))
                    {
                        var labelNull = body.DefineLabel();
                        var labelNotNull = body.DefineLabel();
                        body.Emit(OpCodes.Dup);
                        body.Emit(OpCodes.Brfalse, labelNull);
                        // We need to directly resolve from all constructors because on mono GetConstructor can cause the following issue:
                        // `Missing field layout info for ...`
                        // This is caused by GetConstructor calling RuntimeTypeHandle.CanCastTo which can fail since right now unhollower emits ALL fields which appear to now work properly
                        body.Emit(OpCodes.Newobj, type.GetConstructors().FirstOrDefault(ci =>
                        {
                            var ps = ci.GetParameters();
                            return ps.Length == 1 && ps[0].ParameterType == typeof(IntPtr);
                        })!);
                        body.Emit(OpCodes.Br, labelNotNull);
                        body.MarkLabel(labelNull);
                        body.Emit(OpCodes.Pop);
                        body.Emit(OpCodes.Ldnull);
                        body.MarkLabel(labelNotNull);
                    }
                }
                if (parameter.IsByRef)
                {
                    var elemType = parameter.GetElementType();

                    indirectVariables[i] = body.DeclareLocal(elemType);

                    body.Emit(OpCodes.Ldind_I);
                    HandleTypeConversion(elemType);
                    body.Emit(OpCodes.Stloc, indirectVariables[i]);
                    body.Emit(OpCodes.Ldloca, indirectVariables[i]);
                }
                else HandleTypeConversion(parameter);
            }

            body.Emit(OpCodes.Call, monoMethod);
            LocalBuilder managedReturnVariable = null;
            if (monoMethod.ReturnType != typeof(void))
            {
                managedReturnVariable = body.DeclareLocal(monoMethod.ReturnType);
                body.Emit(OpCodes.Stloc, managedReturnVariable);
            }

            for (var i = 1; i < managedParameters.Length; i++)
            {
                LocalBuilder variable = indirectVariables[i];
                if (variable == null)
                    continue;
                body.Emit(OpCodes.Ldarg_S, i);
                body.Emit(OpCodes.Ldloc, variable);
                var directType = managedParameters[i].GetElementType();
                if (directType == typeof(string))
                    body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.ManagedStringToIl2Cpp))!);
                else if (!directType.IsValueType)
                    body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.Il2CppObjectBaseToPtr))!);
                body.Emit(InjectorHelpers.StIndOpcodes.TryGetValue(directType, out OpCode stindOpCodde) ? stindOpCodde : OpCodes.Stind_I);
            }
            if (managedReturnVariable != null)
            {
                body.Emit(OpCodes.Ldloc, managedReturnVariable);
                if (monoMethod.ReturnType == typeof(string))
                {
                    body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.ManagedStringToIl2Cpp))!);
                }
                else if (!monoMethod.ReturnType.IsValueType)
                {
                    body.Emit(OpCodes.Call, typeof(IL2CPP).GetMethod(nameof(IL2CPP.Il2CppObjectBaseToPtr))!);
                }
            }
            body.Emit(OpCodes.Ret);

            var exceptionLocal = body.DeclareLocal(typeof(Exception));
            body.BeginCatchBlock(typeof(Exception));
            body.Emit(OpCodes.Stloc, exceptionLocal);
            body.Emit(OpCodes.Ldstr, "Exception in IL2CPP-to-Managed trampoline, not passing it to il2cpp: ");
            body.Emit(OpCodes.Ldloc, exceptionLocal);
            body.Emit(OpCodes.Callvirt, typeof(object).GetMethod(nameof(ToString))!);
            body.Emit(OpCodes.Call, typeof(string).GetMethod(nameof(string.Concat), new[] { typeof(string), typeof(string) })!);
            body.Emit(OpCodes.Call, typeof(LogSupport).GetMethod(nameof(LogSupport.Error))!);

            body.EndExceptionBlock();

            if (monoMethod.ReturnType != typeof(void))
            {
                if (monoMethod.ReturnType.IsValueType)
                {
                    if (monoMethod.ReturnType.IsPrimitive)
                    {
                        if (monoMethod.ReturnType == typeof(float))
                            body.Emit(OpCodes.Ldc_R4, 0);
                        else if (monoMethod.ReturnType == typeof(double))
                            body.Emit(OpCodes.Ldc_R8, 0);
                        else
                        {
                            body.Emit(OpCodes.Ldc_I4_0);
                            if (monoMethod.ReturnType == typeof(long) || monoMethod.ReturnType == typeof(ulong))
                            {
                                body.Emit(OpCodes.Conv_I8);
                            }
                        }
                    }
                    else
                    {
                        var local = body.DeclareLocal(monoMethod.ReturnType);

                        body.Emit(OpCodes.Ldloca_S, local);
                        body.Emit(OpCodes.Initobj, monoMethod.ReturnType);
                        body.Emit(OpCodes.Ldloc_S, local);
                    }
                }
                else
                {
                    body.Emit(OpCodes.Ldc_I4_0);
                    body.Emit(OpCodes.Conv_I);
                }
            }
            body.Emit(OpCodes.Ret);

            var @delegate = method.CreateDelegate(delegateType);
            GCHandle.Alloc(@delegate); // pin it forever
            return @delegate;
        }

        private static string ExtractSignature(MethodInfo monoMethod)
        {
            var builder = new StringBuilder();
            builder.Append(monoMethod.ReturnType.NativeType().Name);
            builder.Append(monoMethod.IsStatic ? "" : "This");
            foreach (var parameterInfo in monoMethod.GetParameters())
                builder.Append(parameterInfo.ParameterType.NativeType().Name);
            return builder.ToString();
        }

        private static Type NativeType(this Type type)
        {
            return type.IsValueType ? type : typeof(IntPtr);
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate Il2CppMethodInfo* GenericGetMethodDelegate(Il2CppGenericMethod* gmethod, bool copyMethodPtr);
        private static volatile GenericGetMethodDelegate ourOriginalGenericGetMethod;

        private static void HookGenericMethodGetMethod()
        {
            var getVirtualMethodEntryPoint = InjectorHelpers.GetIl2CppExport(nameof(IL2CPP.il2cpp_object_get_virtual_method));
            LogSupport.Trace($"il2cpp_object_get_virtual_method entry address: {getVirtualMethodEntryPoint}");

            var getVirtualMethodMethod = XrefScannerLowLevel.JumpTargets(getVirtualMethodEntryPoint).Single();
            LogSupport.Trace($"Xref scan target 1: {getVirtualMethodMethod}");

            var targetMethod = XrefScannerLowLevel.JumpTargets(getVirtualMethodMethod).Last();
            LogSupport.Trace($"Xref scan target 2: {targetMethod}");

            if (targetMethod == IntPtr.Zero)
                return;

            var targetTargets = XrefScannerLowLevel.JumpTargets(targetMethod).Take(2).ToList();
            if (targetTargets.Count == 1) // U2021.2.0+, there's additional shim that takes 3 parameters
                targetMethod = targetTargets[0];

            ourOriginalGenericGetMethod = Detour.Detour(targetMethod, new GenericGetMethodDelegate(GenericGetMethodPatch));
            LogSupport.Trace("il2cpp_class_from_il2cpp_type patched");
        }

        private static System.Type RewriteType(Type type)
        {
            if (type.IsValueType && !type.IsEnum)
                return type;

            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                if (elementType!.FullName == "System.String")
                {
                    return typeof(Il2CppStringArray);
                }

                var convertedElementType = RewriteType(elementType);
                if (elementType.IsGenericParameter)
                {
                    return typeof(Il2CppArrayBase<>).MakeGenericType(convertedElementType);
                }

                return (convertedElementType.IsValueType ? typeof(Il2CppStructArray<>) : typeof(Il2CppReferenceArray<>)).MakeGenericType(convertedElementType);
            }

            if (type.FullName!.StartsWith("System"))
            {
                var fullName = $"Il2Cpp{type.FullName}";
                var resolvedType = Type.GetType($"{fullName}, Il2Cpp{type.Assembly.GetName().Name}", false);
                if (resolvedType != null)
                    return resolvedType;

                return AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.GetType(fullName, false))
                    .First(t => t != null);
            }

            return type;
        }

        private static System.Type SystemTypeFromIl2CppType(Il2CppTypeStruct* typePointer)
        {
            var klass = UnityVersionHandler.Wrap(InjectorHelpers.ClassFromIl2CppType(typePointer));
            var assembly = UnityVersionHandler.Wrap(UnityVersionHandler.Wrap(klass.Image).Assembly);

            var fullName = new StringBuilder();

            var namespaceName = Marshal.PtrToStringAnsi(klass.Namespace);
            if (!string.IsNullOrEmpty(namespaceName))
            {
                fullName.Append(namespaceName);
                fullName.Append('.');
            }

            fullName.Append(Marshal.PtrToStringAnsi(klass.Name));

            fullName.Append(", ");
            fullName.Append(Marshal.PtrToStringAnsi(assembly.Name.Name));

            var type = Type.GetType(fullName.ToString()) ?? throw new NullReferenceException($"Couldn't find System.Type for Il2Cpp type: {fullName}");
            return RewriteType(type);
        }

        private static Il2CppMethodInfo* GenericGetMethodPatch(Il2CppGenericMethod* gmethod, bool copyMethodPtr)
        {
            if (InflatedMethodFromContextDictionary.TryGetValue((IntPtr)gmethod->methodDefinition, out var methods))
            {
                var instancePointer = gmethod->context.method_inst;
                if (methods.Item2.TryGetValue((IntPtr)instancePointer, out var inflatedMethodPointer))
                    return (Il2CppMethodInfo*)inflatedMethodPointer;

                var typeArguments = new Type[instancePointer->type_argc];
                for (var i = 0; i < instancePointer->type_argc; i++)
                    typeArguments[i] = SystemTypeFromIl2CppType(instancePointer->type_argv[i]);
                var inflatedMethod = methods.Item1.MakeGenericMethod(typeArguments);
                LogSupport.Trace("Inflated method: " + inflatedMethod.Name);
                inflatedMethodPointer = (IntPtr)ConvertMethodInfo(inflatedMethod, UnityVersionHandler.Wrap(UnityVersionHandler.Wrap(gmethod->methodDefinition).Class));
                methods.Item2.Add((IntPtr)instancePointer, inflatedMethodPointer);

                return (Il2CppMethodInfo*)inflatedMethodPointer;
            }
            return ourOriginalGenericGetMethod(gmethod, copyMethodPtr);
        }

        public static IManagedDetour Detour = new DoHookDetour();
        [Obsolete("Set Detour instead")]
        public static Action<IntPtr, IntPtr> DoHook;

        private static readonly ConcurrentDictionary<(Type type, FieldAttributes attrs), IntPtr> _injectedFieldTypes = new();
        private static readonly VoidCtorDelegate FinalizeDelegate = Finalize;

        private class DoHookDetour : IManagedDetour
        {
            // In some cases garbage collection of delegates can release their native function pointer too - keep all of them alive to avoid that
            // ReSharper disable once CollectionNeverQueried.Local
            private static readonly List<object> PinnedDelegates = new List<object>();

            public T Detour<T>(IntPtr @from, T to) where T : Delegate
            {
                IntPtr* targetVarPointer = &from;
                PinnedDelegates.Add(to);
                DoHook((IntPtr)targetVarPointer, Marshal.GetFunctionPointerForDelegate(to));
                return Marshal.GetDelegateForFunctionPointer<T>(from);
            }
        }
    }

    public interface IManagedDetour
    {
        /// <summary>
        /// Patch the native function at address specified in `from`, replacing it with `to`, and return a delegate to call the original native function
        /// </summary>
        T Detour<T>(IntPtr from, T to) where T : Delegate;
    }
}
