using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using LibCpp2IL.BinaryStructures;

namespace Il2CppInterop.Generator;

public class InitializationClassProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "initialization_class_injector";
    public override string Name => "Inject initialization class into the Cpp2IL context system";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var runClassConstructor = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.Runtime.CompilerServices.RuntimeHelpers")
            .Methods.First(m => m.Name == nameof(RuntimeHelpers.RunClassConstructor) && m.Parameters[0].ParameterType != appContext.SystemTypes.SystemIntPtrType);
        var il2CppClassPointerStore = appContext.ResolveTypeOrThrow(typeof(Il2CppClassPointerStore<>));
        var classPointerField = il2CppClassPointerStore.GetFieldByName("NativeClassPtr");

        var il2CppStaticClass = appContext.ResolveTypeOrThrow(typeof(IL2CPP));
        var getIl2CppNestedType = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppNestedType));
        var getIl2CppClass = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppClass));
        var getIl2CppGenericInstanceType = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppGenericInstanceType));
        var il2CppRuntimeClassInit = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.il2cpp_runtime_class_init));
        var getIl2CppField = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppField));
        var il2CppFieldGetOffset = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.il2cpp_field_get_offset));
        var getIl2CppMethod = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppMethod));
        var getIl2CppMethodByToken = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppMethodByToken));
        var getIl2CppGenericInstanceMethod = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2CppGenericInstanceMethod));
        var getIl2CppValueSize = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.GetIl2cppValueSize));
        var resolveICall = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.ResolveICall));

        var multicastDelegateType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.MulticastDelegate");
        var asyncCallbackType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.AsyncCallback");
        var iasyncResultType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.IAsyncResult");

        var tokenLessMethodCount = 0;

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            for (var i = 0; i < assembly.Types.Count; i++)
            {
                var type = assembly.Types[i];

                if (type.IsInjected)
                    continue;

                var typeInfo = type.GetExtraData<Il2CppTypeInfo>();
                Debug.Assert(typeInfo is not null);

                var initializationType = assembly.InjectType(
                    "Il2CppInterop.Generated",
                    $"Il2CppInternals_{HashString(type.FullName):x16}",
                    appContext.SystemTypes.SystemObjectType,
                    TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
                initializationType.IsInjected = true;
                foreach (var genericParameter in type.GenericParameters)
                {
                    initializationType.GenericParameters.Add(new GenericParameterTypeAnalysisContext(
                        genericParameter.Name,
                        genericParameter.Index,
                        genericParameter.Type,
                        genericParameter.Attributes & GenericParameterAttributes.AllowByRefLike,
                        initializationType));
                }

                AddInstructionsToStaticConstructor(type, initializationType, runClassConstructor);

                // Initialization static constructor
                {
                    var staticConstructor = new InjectedMethodAnalysisContext(
                        initializationType,
                        ".cctor",
                        type.AppContext.SystemTypes.SystemVoidType,
                        MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                        []);
                    initializationType.Methods.Add(staticConstructor);

                    var instructions = new List<Instruction>();
                    var localVariables = new List<LocalVariable>();

                    var typeToInitialize = initializationType.GenericParameters.Count == 0
                        ? type
                        : type.MakeGenericInstanceType(initializationType.GenericParameters);

                    var concreteClassPointerField = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([typeToInitialize]));
                    if (typeToInitialize.DeclaringType is not null)
                    {
                        // Ensure declaring type is initialized first
                        instructions.Add(new Instruction(OpCodes.Ldtoken, typeToInitialize.DeclaringType));
                        instructions.Add(new Instruction(OpCodes.Call, runClassConstructor));

                        // Il2CppClassPointerStore<NestedClass>.NativeClassPtr = IL2CPP.GetIl2CppNestedType(Il2CppClassPointerStore<DeclaringType>.NativeClassPtr, "NestedClass");
                        var declaringTypeClassPointerField = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([typeToInitialize.DeclaringType]));
                        instructions.Add(new Instruction(OpCodes.Ldsfld, declaringTypeClassPointerField));
                        instructions.Add(new Instruction(OpCodes.Ldstr, type.DefaultName));// typeToInitialize can have the wrong DefaultName
                        instructions.Add(new Instruction(OpCodes.Call, getIl2CppNestedType));
                    }
                    else
                    {
                        // Il2CppClassPointerStore<Class>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "Class");
                        instructions.Add(new Instruction(OpCodes.Ldstr, $"{assembly.DefaultName}.dll"));
                        instructions.Add(new Instruction(OpCodes.Ldstr, type.DefaultNamespace));
                        instructions.Add(new Instruction(OpCodes.Ldstr, type.DefaultName));
                        instructions.Add(new Instruction(OpCodes.Call, getIl2CppClass));
                    }
                    if (type.GenericParameters.Count > 0)
                    {
                        instructions.Add(new Instruction(OpCodes.Ldc_I4, type.GenericParameters.Count));
                        instructions.Add(new Instruction(OpCodes.Newarr, appContext.SystemTypes.SystemIntPtrType));
                        for (var j = 0; j < type.GenericParameters.Count; j++)
                        {
                            instructions.Add(new Instruction(OpCodes.Dup));
                            instructions.Add(new Instruction(OpCodes.Ldc_I4, j));
                            var genericParameter = initializationType.GenericParameters[j];
                            var classPointerForGenericParameter = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([genericParameter]));
                            instructions.Add(new Instruction(OpCodes.Ldsfld, classPointerForGenericParameter));
                            instructions.Add(new Instruction(OpCodes.Stelem_I));
                        }
                        instructions.Add(new Instruction(OpCodes.Call, getIl2CppGenericInstanceType));
                    }
                    instructions.Add(new Instruction(OpCodes.Stsfld, concreteClassPointerField));

                    // IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<Class>.NativeClassPtr);
                    instructions.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                    instructions.Add(new Instruction(OpCodes.Call, il2CppRuntimeClassInit));

                    // Size = IL2CPP.il2cpp_class_value_size(Il2CppClassPointerStore<Class>.NativeClassPtr, ref align);
                    if (typeInfo.Blittability != TypeBlittability.ReferenceType)
                    {
                        var sizeStore = initializationType.InjectFieldContext(
                            "Size",
                            appContext.SystemTypes.SystemInt32Type,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        type.SizeStorage = sizeStore;

                        FieldAnalysisContext instantiatedSizeStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(sizeStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : sizeStore;

                        instructions.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                        instructions.Add(new Instruction(OpCodes.Call, getIl2CppValueSize));
                        instructions.Add(new Instruction(OpCodes.Stsfld, instantiatedSizeStore));
                    }

                    // FieldOffset_Instance_0 = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Class>.NativeClassPtr, "field_name"));
                    for (var index = 0; index < typeInfo.InstanceFields.Count; index++)
                    {
                        var instanceField = typeInfo.InstanceFields[index];

                        var infoStore = initializationType.InjectFieldContext(
                            $"FieldInfoPtr_Instance_{index}",
                            appContext.SystemTypes.SystemIntPtrType,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        instanceField.FieldInfoAddressStorage = infoStore;

                        FieldAnalysisContext instantiatedInfoStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(infoStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : infoStore;

                        var offsetStore = initializationType.InjectFieldContext(
                            $"FieldOffset_Instance_{index}",
                            appContext.SystemTypes.SystemInt32Type,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        instanceField.OffsetStorage = offsetStore;

                        FieldAnalysisContext instantiatedOffsetStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(offsetStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : offsetStore;

                        instructions.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                        instructions.Add(new Instruction(OpCodes.Ldstr, instanceField.DefaultName));
                        instructions.Add(new Instruction(OpCodes.Call, getIl2CppField));
                        instructions.Add(new Instruction(OpCodes.Dup));
                        instructions.Add(new Instruction(OpCodes.Stsfld, instantiatedInfoStore));
                        instructions.Add(new Instruction(OpCodes.Call, il2CppFieldGetOffset));
                        instructions.Add(new Instruction(OpCodes.Conv_I4));
                        instructions.Add(new Instruction(OpCodes.Stsfld, instantiatedOffsetStore));
                    }

                    // FieldInfoPtr_Static_0 = IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Class>.NativeClassPtr, "field_name");
                    for (var index = 0; index < typeInfo.StaticFields.Count; index++)
                    {
                        var staticField = typeInfo.StaticFields[index];
                        var infoStore = initializationType.InjectFieldContext(
                            $"FieldInfoPtr_Static_{index}",
                            appContext.SystemTypes.SystemIntPtrType,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        staticField.FieldInfoAddressStorage = infoStore;

                        FieldAnalysisContext instantiatedInfoStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(infoStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : infoStore;

                        var offsetStore = initializationType.InjectFieldContext(
                            $"FieldOffset_Static_{index}",
                            appContext.SystemTypes.SystemInt32Type,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        staticField.OffsetStorage = offsetStore;

                        FieldAnalysisContext instantiatedOffsetStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(offsetStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : offsetStore;

                        instructions.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                        instructions.Add(new Instruction(OpCodes.Ldstr, staticField.DefaultName));
                        instructions.Add(new Instruction(OpCodes.Call, getIl2CppField));
                        instructions.Add(new Instruction(OpCodes.Dup));
                        instructions.Add(new Instruction(OpCodes.Stsfld, instantiatedInfoStore));
                        instructions.Add(new Instruction(OpCodes.Call, il2CppFieldGetOffset));
                        instructions.Add(new Instruction(OpCodes.Conv_I4));
                        instructions.Add(new Instruction(OpCodes.Stsfld, instantiatedOffsetStore));
                    }

                    // MethodInfoPtr_0
                    for (var index = 0; index < type.Methods.Count; index++)
                    {
                        var method = type.Methods[index];

                        if (method.IsUnstripped || method.IsInjected)
                            continue;

                        var methodInfoStore = initializationType.InjectFieldContext(
                                                $"MethodInfoPtr_{index}",
                                                appContext.SystemTypes.SystemIntPtrType,
                                                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        method.MethodInfoField = methodInfoStore;

                        FieldAnalysisContext concreteMethodInfoStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(methodInfoStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : methodInfoStore;

                        if (method.Token == 0)
                        {
                            tokenLessMethodCount++;

                            instructions.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                            instructions.Add(new Instruction(method.GenericParameters.Count == 0 ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1));
                            instructions.Add(new Instruction(OpCodes.Ldstr, method.DefaultName));
                            instructions.Add(new Instruction(OpCodes.Ldstr, method.DefaultReturnType.DefaultFullName));
                            instructions.Add(new Instruction(OpCodes.Ldc_I4, method.Parameters.Count));
                            instructions.Add(new Instruction(OpCodes.Newarr, method.AppContext.SystemTypes.SystemStringType));

                            for (var parameterIndex = 0; i < method.Parameters.Count; i++)
                            {
                                instructions.Add(new Instruction(OpCodes.Dup));
                                instructions.Add(new Instruction(OpCodes.Ldc_I4, parameterIndex));
                                instructions.Add(new Instruction(OpCodes.Ldstr, method.Parameters[i].DefaultParameterType.DefaultFullName));
                                instructions.Add(new Instruction(OpCodes.Stelem_Ref));
                            }

                            instructions.Add(new Instruction(OpCodes.Call, getIl2CppMethod));
                        }
                        else
                        {
                            instructions.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                            instructions.Add(new Instruction(OpCodes.Ldc_I4, unchecked((int)method.Token)));
                            instructions.Add(new Instruction(OpCodes.Call, getIl2CppMethodByToken));
                        }
                        instructions.Add(new Instruction(OpCodes.Stsfld, concreteMethodInfoStore));

                        if (method.GenericParameters.Count > 0)
                        {
                            var methodInfoPtrGenericClass = initializationType.InjectNestedType(
                                $"MethodInfoPtrGeneric_{index}",
                                appContext.SystemTypes.SystemObjectType,
                                TypeAttributes.NestedAssembly | TypeAttributes.Abstract | TypeAttributes.Sealed);
                            methodInfoPtrGenericClass.IsInjected = true;
                            methodInfoPtrGenericClass.CopyGenericParameters(initializationType);
                            foreach (var genericParameter in method.GenericParameters)
                            {
                                methodInfoPtrGenericClass.GenericParameters.Add(new GenericParameterTypeAnalysisContext(
                                    genericParameter.Name,
                                    methodInfoPtrGenericClass.GenericParameters.Count,
                                    Il2CppTypeEnum.IL2CPP_TYPE_VAR,
                                    genericParameter.Attributes & GenericParameterAttributes.AllowByRefLike,
                                    methodInfoPtrGenericClass));
                            }

                            var methodInfoPtrGenericField = methodInfoPtrGenericClass.InjectFieldContext(
                                "Pointer",
                                appContext.SystemTypes.SystemIntPtrType,
                                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                            method.MethodInfoField = methodInfoPtrGenericField; // A generic method's real MethodInfoField is the generically instantiated one.

                            var methodInfoPtrGenericStaticConstructor = new InjectedMethodAnalysisContext(
                                methodInfoPtrGenericClass,
                                ".cctor",
                                appContext.SystemTypes.SystemVoidType,
                                MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                []);
                            methodInfoPtrGenericClass.Methods.Add(methodInfoPtrGenericStaticConstructor);

                            FieldAnalysisContext concreteMethodInfoStore2 = initializationType.GenericParameters.Count > 0
                                ? new ConcreteGenericFieldAnalysisContext(methodInfoStore, initializationType.MakeGenericInstanceType(methodInfoPtrGenericClass.GenericParameters.Take(initializationType.GenericParameters.Count)))
                                : methodInfoStore;

                            var instructions2 = new List<Instruction>();
                            instructions2.Add(new Instruction(OpCodes.Ldsfld, concreteMethodInfoStore2));
                            instructions2.Add(new Instruction(OpCodes.Ldsfld, concreteClassPointerField));
                            instructions2.Add(new Instruction(OpCodes.Ldc_I4, method.GenericParameters.Count));
                            instructions2.Add(new Instruction(OpCodes.Newarr, appContext.SystemTypes.SystemIntPtrType));
                            for (var j = 0; j < method.GenericParameters.Count; j++)
                            {
                                instructions2.Add(new Instruction(OpCodes.Dup));
                                instructions2.Add(new Instruction(OpCodes.Ldc_I4, j));
                                var genericParameter = methodInfoPtrGenericClass.GenericParameters[j + initializationType.GenericParameters.Count];
                                var classPointerForGenericParameter = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([genericParameter]));
                                instructions2.Add(new Instruction(OpCodes.Ldsfld, classPointerForGenericParameter));
                                instructions2.Add(new Instruction(OpCodes.Stelem_I));
                            }
                            instructions2.Add(new Instruction(OpCodes.Call, getIl2CppGenericInstanceMethod));
                            instructions2.Add(new Instruction(OpCodes.Stsfld, new ConcreteGenericFieldAnalysisContext(methodInfoPtrGenericField, methodInfoPtrGenericClass.MakeGenericInstanceType(methodInfoPtrGenericClass.GenericParameters))));
                            instructions2.Add(new Instruction(OpCodes.Ret));

                            methodInfoPtrGenericStaticConstructor.PutExtraData(new NativeMethodBody()
                            {
                                Instructions = instructions2,
                            });
                        }
                    }

                    // Internal call methods
                    for (var index = 0; index < type.Methods.Count; index++)
                    {
                        var method = type.Methods[index];

                        if (!method.IsUnstripped || !method.DefaultImplAttributes.HasFlag(MethodImplAttributes.InternalCall))
                            continue;

                        Debug.Assert(!method.HasExtraData<OriginalMethodBody>());
                        Debug.Assert(!method.HasExtraData<TranslatedMethodBody>());
                        Debug.Assert(!method.HasExtraData<NativeMethodBody>());
                        Debug.Assert(method.GenericParameters.Count == 0 && type.GenericParameters.Count == 0, "Internal calls cannot be generic.");

                        // ICall_Delegate_Type_0
                        TypeAnalysisContext delegateType;
                        MethodAnalysisContext invokeMethod;
                        {
                            delegateType = initializationType.InjectNestedType(
                                $"ICall_Delegate_Type_{index}",
                                multicastDelegateType);

                            List<TypeAnalysisContext> parameterTypes = method.Parameters.Select(p => p.ParameterType).ToList();
                            List<string> parameterNames = method.Parameters.Select(p => p.Name).ToList();

                            // Constructor
                            {
                                delegateType.Methods.Add(new InjectedMethodAnalysisContext(
                                    delegateType,
                                    ".ctor",
                                    appContext.SystemTypes.SystemVoidType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                    [appContext.SystemTypes.SystemObjectType, appContext.SystemTypes.SystemIntPtrType],
                                    defaultImplAttributes: MethodImplAttributes.Runtime)
                                {
                                    RuntimeImplemented = true,
                                });
                            }

                            // Invoke
                            {
                                invokeMethod = new InjectedMethodAnalysisContext(
                                    delegateType,
                                    "Invoke",
                                    appContext.SystemTypes.SystemVoidType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                                    parameterTypes.ToArray(),
                                    parameterNames.ToArray(),
                                    defaultImplAttributes: MethodImplAttributes.Runtime)
                                {
                                    RuntimeImplemented = true,
                                };
                                delegateType.Methods.Add(invokeMethod);
                            }

                            // BeginInvoke
                            {
                                delegateType.Methods.Add(new InjectedMethodAnalysisContext(
                                    delegateType,
                                    "BeginInvoke",
                                    iasyncResultType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                                    parameterTypes.Append(asyncCallbackType).Append(appContext.SystemTypes.SystemObjectType).ToArray(),
                                    parameterNames.Append("callback").Append("object").ToArray(),
                                    defaultImplAttributes: MethodImplAttributes.Runtime)
                                {
                                    RuntimeImplemented = true,
                                });
                            }

                            // EndInvoke
                            {
                                delegateType.Methods.Add(new InjectedMethodAnalysisContext(
                                    delegateType,
                                    "EndInvoke",
                                    appContext.SystemTypes.SystemVoidType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                                    [iasyncResultType],
                                    ["result"],
                                    defaultImplAttributes: MethodImplAttributes.Runtime)
                                {
                                    RuntimeImplemented = true,
                                });
                            }
                        }

                        // ICall_Delegate_Field_0
                        FieldAnalysisContext delegateField;
                        {
                            delegateField = initializationType.InjectFieldContext(
                                $"ICall_Delegate_Field_{index}",
                                delegateType,
                                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        }

                        // Static constructor instructions
                        {
                            instructions.Add(new Instruction(OpCodes.Ldstr, $"{type.DefaultFullName}::{method.DefaultName}"));
                            instructions.Add(new Instruction(OpCodes.Call, new ConcreteGenericMethodAnalysisContext(resolveICall, [], [delegateType])));
                            instructions.Add(new Instruction(OpCodes.Stsfld, delegateField));
                        }
                    }

                    instructions.Add(new Instruction(OpCodes.Ret));

                    staticConstructor.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = instructions,
                        LocalVariables = localVariables.Count > 0 ? localVariables : [],
                    });
                }
            }
        }

        Logger.Info($"Tokenless method count: {tokenLessMethodCount}", nameof(InitializationClassProcessingLayer));

        // Il2CppInterop.Initialization.dll
        {
            var injectedAssembly = appContext.InjectAssembly("Il2CppInterop.Initialization");
            injectedAssembly.IsInjected = true;

            var initializationType = injectedAssembly.InjectType(
                "Il2CppInterop.Initialization",
                "Il2CppInitialization",
                appContext.SystemTypes.SystemObjectType,
                TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);

            var initializeMethod = initializationType.InjectMethodContext(
                "Initialize",
                appContext.SystemTypes.SystemVoidType,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                []);

            var instructions = new List<Instruction>();

            var typeConverter = TypeConversionVisitor.Create(appContext);

            var processedTypes = new HashSet<TypeAnalysisContext>(TypeAnalysisContextEqualityComparer.Instance);
            // Ensure all generic instantiations are initialized
            foreach (var il2CppType in appContext.Binary.AllTypes)
            {
                var typeContext = injectedAssembly.ResolveIl2CppType(il2CppType);
                if (InvalidTypeChecker.ContainsInvalidType(typeContext))
                    continue;

                if (typeContext is not ReferencedTypeAnalysisContext && typeContext.GenericParameters.Count > 0)
                    continue; // Skip open generics

                typeContext = typeConverter.Replace(typeContext);

                if (!processedTypes.Add(typeContext))
                    continue;

                // Ensure the type is initialized
                instructions.Add(new Instruction(OpCodes.Ldtoken, typeContext));
                instructions.Add(new Instruction(OpCodes.Call, runClassConstructor));
            }

            instructions.Add(new Instruction(OpCodes.Ret));

            initializeMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions,
            });
        }
    }

    private static void AddInstructionsToStaticConstructor(TypeAnalysisContext type, InjectedTypeAnalysisContext initializationType, MethodAnalysisContext runClassConstructor)
    {
        var instructions = type.GetOrCreateStaticConstructorInstructions();

        var typeToInitialize = type.GenericParameters.Count == 0 ? initializationType : (TypeAnalysisContext)initializationType.MakeGenericInstanceType(type.GenericParameters);

        instructions.Add(new Instruction(OpCodes.Ldtoken, typeToInitialize));
        instructions.Add(new Instruction(OpCodes.Call, runClassConstructor));
    }

    private static ulong HashString(ReadOnlySpan<char> chars)
    {
        var bytes = MemoryMarshal.AsBytes(chars);
        Span<byte> hash = stackalloc byte[MD5.HashSizeInBytes];
        MD5.HashData(bytes, hash);
        return BinaryPrimitives.ReadUInt64LittleEndian(hash);
    }

    private sealed class InvalidTypeChecker : BooleanOrTypeVisitor
    {
        public static InvalidTypeChecker Instance { get; } = new InvalidTypeChecker();

        public static bool ContainsInvalidType(TypeAnalysisContext type)
        {
            return Instance.Visit(type);
        }

        public override bool Visit(BoxedTypeAnalysisContext type) => true;
        public override bool Visit(ByRefTypeAnalysisContext type) => true;
        public override bool Visit(GenericParameterTypeAnalysisContext type) => true;
        public override bool Visit(PinnedTypeAnalysisContext type) => true;
        public override bool Visit(SentinelTypeAnalysisContext type) => true;
    }
}
