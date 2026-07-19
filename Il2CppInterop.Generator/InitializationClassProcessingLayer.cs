using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Cpp2IL.Core.Utils;
using Il2CppInterop.Common;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class InitializationClassProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "initialization_class_injector";
    public override string Name => "Inject initialization class into the Cpp2IL context system";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var runClassConstructor = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.Runtime.CompilerServices.RuntimeHelpers")
            .Methods.First(m => m.Name == nameof(RuntimeHelpers.RunClassConstructor) && m.Parameters[0].ParameterType != appContext.SystemTypes.SystemIntPtrType);

        var fieldAccessClass = appContext.ResolveTypeOrThrow(typeof(FieldAccess));
        var getFieldInfo = fieldAccessClass.GetMethodByName(nameof(FieldAccess.GetFieldInfo));
        var getFieldOffset = fieldAccessClass.GetMethodByName(nameof(FieldAccess.GetFieldOffset));

        var resolveICall = appContext.ResolveTypeOrThrow(typeof(RuntimeInvoke)).GetMethodByName(nameof(RuntimeInvoke.ResolveICall));

        var generationInternalsType = appContext.ResolveTypeOrThrow(typeof(GenerationInternals));
        var getIl2CppMethod = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppMethod));
        var getIl2CppMethodByToken = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppMethodByToken));
        var getIl2CppGenericInstanceType = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppGenericInstanceType));
        var getIl2CppNestedType = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppNestedType));
        var getIl2CppClass = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppClass));
        var il2CppRuntimeClassInit = generationInternalsType.GetMethodByName(nameof(GenerationInternals.Il2CppRuntimeClassInit));
        var getIl2CppGenericInstanceMethod = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppGenericInstanceMethod));
        var getIl2CppValueSize = generationInternalsType.GetMethodByName(nameof(GenerationInternals.GetIl2CppValueSize));

        var typeInjector = appContext.ResolveTypeOrThrow(typeof(TypeInjector));
        var registerTypeInIl2Cpp = typeInjector.Methods.Single(m =>
        {
            return m.Name == nameof(TypeInjector.RegisterTypeInIl2Cpp) && m.Parameters.Count is 0 && m.GenericParameters.Count == 1;
        });

        var multicastDelegateType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.MulticastDelegate");
        var asyncCallbackType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.AsyncCallback");
        var iasyncResultType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.IAsyncResult");

        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_CopyFrom = byReference.GetMethodByName(nameof(ByReference<>.CopyFrom));
        var byReference_CopyTo = byReference.GetMethodByName(nameof(ByReference<>.CopyTo));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");

        var il2CppType = appContext.ResolveTypeOrThrow(typeof(Il2CppType));
        var il2CppType_SizeOf = il2CppType.GetMethodByName(nameof(Il2CppType.SizeOf));
        var il2CppType_GetClassPointer = il2CppType.Methods.Single(m => m.Name == nameof(Il2CppType.GetClassPointer) && m.GenericParameters.Count == 1);
        var il2CppType_SetClassPointer = il2CppType.Methods.Single(m => m.Name == nameof(Il2CppType.SetClassPointer) && m.GenericParameters.Count == 1);

        var il2CppTypeAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeAttribute));
        var il2CppTypeAttributeConstructor = il2CppTypeAttribute.GetMethodByName(".ctor");
        var il2CppTypeAttributeNamespaceProperty = il2CppTypeAttribute.Properties.Single(p => p.Name == nameof(Il2CppTypeAttribute.Namespace));
        var il2CppTypeAttributeNameProperty = il2CppTypeAttribute.Properties.Single(p => p.Name == nameof(Il2CppTypeAttribute.Name));

        var il2CppAssemblyAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppAssemblyAttribute));
        var il2CppAssemblyAttributeConstructor = il2CppAssemblyAttribute.GetMethodByName(".ctor");
        var il2CppAssemblyAttributeNameProperty = il2CppAssemblyAttribute.Properties.Single(p => p.Name == nameof(Il2CppAssemblyAttribute.Name));

        var tokenLessMethodCount = 0;

        // 2 pointers
        var headerSize = (object)(appContext.Binary.is32Bit ? 8 : 16);

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            // Il2CppAssemblyAttribute
            {
                var attribute = new AnalyzedCustomAttribute(il2CppAssemblyAttributeConstructor);
                attribute.Properties.Add(new CustomAttributeProperty(il2CppAssemblyAttributeNameProperty, new CustomAttributePrimitiveParameter(assembly.ImageName, attribute, CustomAttributeParameterKind.Property, 0)));
                assembly.CustomAttributes ??= new(1);
                assembly.CustomAttributes.Add(attribute);
            }

            for (var i = 0; i < assembly.Types.Count; i++)
            {
                var type = assembly.Types[i];

                if (type.IsInjected)
                    continue;

                var initializationType = assembly.InjectType(
                    "Il2CppInterop.Generated",
                    $"Il2CppInternals_{HashString(type.FullName):x16}",
                    appContext.SystemTypes.SystemObjectType,
                    TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
                initializationType.IsInjected = true;
                initializationType.CopyGenericParameters(type, true, true);

                AddInstructionsToStaticConstructor(type, initializationType, runClassConstructor);

                // Il2CppTypeAttribute
                {
                    var attribute = new AnalyzedCustomAttribute(il2CppTypeAttributeConstructor);
                    attribute.ConstructorParameters.Add(new CustomAttributeTypeParameter(initializationType, attribute, CustomAttributeParameterKind.ConstructorParam, 0));

                    if (type.Namespace != type.DefaultNamespace)
                        attribute.Properties.Add(new CustomAttributeProperty(il2CppTypeAttributeNamespaceProperty, new CustomAttributePrimitiveParameter(type.DefaultNamespace, attribute, CustomAttributeParameterKind.Property, attribute.Properties.Count)));

                    if (type.Name != type.DefaultName)
                        attribute.Properties.Add(new CustomAttributeProperty(il2CppTypeAttributeNameProperty, new CustomAttributePrimitiveParameter(type.DefaultName, attribute, CustomAttributeParameterKind.Property, attribute.Properties.Count)));

                    type.CustomAttributes ??= new(1);
                    type.CustomAttributes.Add(attribute);
                }

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

                    var getClassPointerMethodInstantiated = il2CppType_GetClassPointer.MakeGenericInstanceMethod(typeToInitialize);
                    if (type.IsUnstripped)
                    {
                        instructions.Add(new Instruction(CilOpCodes.Call, registerTypeInIl2Cpp.MakeGenericInstanceMethod(typeToInitialize)));
                    }
                    else
                    {
                        if (typeToInitialize.DeclaringType is not null)
                        {
                            // Ensure declaring type is initialized first
                            instructions.Add(new Instruction(CilOpCodes.Ldtoken, typeToInitialize.DeclaringType));
                            instructions.Add(new Instruction(CilOpCodes.Call, runClassConstructor));

                            // Il2CppType.SetClassPointer<NestedClass>(IL2CPP.GetIl2CppNestedType(Il2CppType.GetClassPointer<DeclaringType>(), "NestedClass"));
                            instructions.Add(new Instruction(CilOpCodes.Call, il2CppType_GetClassPointer.MakeGenericInstanceMethod(typeToInitialize.DeclaringType)));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, type.DefaultName));// typeToInitialize can have the wrong DefaultName
                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppNestedType));
                        }
                        else
                        {
                            // Il2CppType.SetClassPointer<Class>(IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "Class"));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, assembly.ImageName));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, type.DefaultNamespace));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, type.DefaultName));
                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppClass));
                        }
                        if (type.GenericParameters.Count > 0)
                        {
                            instructions.Add(new Instruction(CilOpCodes.Ldc_I4, type.GenericParameters.Count));
                            instructions.Add(new Instruction(CilOpCodes.Newarr, appContext.SystemTypes.SystemIntPtrType));
                            for (var j = 0; j < type.GenericParameters.Count; j++)
                            {
                                instructions.Add(new Instruction(CilOpCodes.Dup));
                                instructions.Add(new Instruction(CilOpCodes.Ldc_I4, j));
                                instructions.Add(new Instruction(CilOpCodes.Call, il2CppType_GetClassPointer.MakeGenericInstanceMethod(initializationType.GenericParameters[j])));
                                instructions.Add(new Instruction(CilOpCodes.Stelem_I));
                            }
                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppGenericInstanceType));
                        }
                        instructions.Add(new Instruction(CilOpCodes.Call, il2CppType_SetClassPointer.MakeGenericInstanceMethod(typeToInitialize)));

                        // Il2CppRuntimeClassInit(Il2CppType.GetClassPointer<Class>());
                        instructions.Add(new Instruction(CilOpCodes.Call, getClassPointerMethodInstantiated));
                        instructions.Add(new Instruction(CilOpCodes.Call, il2CppRuntimeClassInit));
                    }

                    // Size = GetIl2CppValueSize(Il2CppType.GetClassPointer<Class>());
                    if (type.IsValueType)
                    {
                        var sizeStore = initializationType.InjectFieldContext(
                            "Size",
                            appContext.SystemTypes.SystemInt32Type,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        type.SizeStorage = sizeStore;

                        FieldAnalysisContext instantiatedSizeStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(sizeStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : sizeStore;

                        instructions.Add(new Instruction(CilOpCodes.Call, getClassPointerMethodInstantiated));
                        instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppValueSize));
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, instantiatedSizeStore));
                    }

                    // FieldOffset_0 = FieldAccess.GetFieldOffset(FieldAccess.GetFieldInfo(Il2CppType.GetClassPointer<Class>(), "field_name"));
                    for (var index = 0; index < type.Fields.Count; index++)
                    {
                        var field = type.Fields[index];

                        if (field.IsInjected)
                            continue;

                        if (field.IsUnstripped && !type.IsUnstripped)
                            continue;

                        field.InitializationClassIndex = index;

                        var infoStore = initializationType.InjectFieldContext(
                            $"FieldInfoPtr_{index}",
                            appContext.SystemTypes.SystemIntPtrType,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        field.FieldInfoAddressStorage = infoStore;

                        FieldAnalysisContext instantiatedInfoStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(infoStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : infoStore;

                        var offsetStore = initializationType.InjectFieldContext(
                            $"FieldOffset_{index}",
                            appContext.SystemTypes.SystemInt32Type,
                            FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);
                        field.OffsetStorage = offsetStore;

                        FieldAnalysisContext instantiatedOffsetStore = initializationType.GenericParameters.Count > 0
                            ? new ConcreteGenericFieldAnalysisContext(offsetStore, initializationType.MakeGenericInstanceType(initializationType.GenericParameters))
                            : offsetStore;

                        instructions.Add(new Instruction(CilOpCodes.Call, getClassPointerMethodInstantiated));
                        instructions.Add(new Instruction(CilOpCodes.Ldstr, field.DefaultName));
                        instructions.Add(new Instruction(CilOpCodes.Call, getFieldInfo));
                        instructions.Add(new Instruction(CilOpCodes.Dup));
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, instantiatedInfoStore));
                        instructions.Add(new Instruction(CilOpCodes.Call, getFieldOffset));
                        instructions.Add(new Instruction(CilOpCodes.Conv_I4));
                        if (type.IsValueType)
                        {
                            // il2cpp_field_get_offset returns offset including the object header
                            // For value types, we need to subtract the header size to get the offset of the field within the struct
                            instructions.Add(new Instruction(CilOpCodes.Ldc_I4, headerSize));
                            instructions.Add(new Instruction(CilOpCodes.Sub));
                        }
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, instantiatedOffsetStore));
                    }

                    // MethodInfoPtr_0
                    for (var index = 0; index < type.Methods.Count; index++)
                    {
                        var method = type.Methods[index];

                        if (method.IsUnstripped || method.IsInjected)
                            continue;

                        method.InitializationClassIndex = index;

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

                            instructions.Add(new Instruction(CilOpCodes.Call, getClassPointerMethodInstantiated));
                            instructions.Add(new Instruction(method.GenericParameters.Count == 0 ? CilOpCodes.Ldc_I4_0 : CilOpCodes.Ldc_I4_1));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, method.DefaultName));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, method.DefaultReturnType.DefaultFullName));
                            instructions.Add(new Instruction(CilOpCodes.Ldc_I4, method.Parameters.Count));
                            instructions.Add(new Instruction(CilOpCodes.Newarr, method.AppContext.SystemTypes.SystemStringType));

                            for (var parameterIndex = 0; i < method.Parameters.Count; i++)
                            {
                                instructions.Add(new Instruction(CilOpCodes.Dup));
                                instructions.Add(new Instruction(CilOpCodes.Ldc_I4, parameterIndex));
                                instructions.Add(new Instruction(CilOpCodes.Ldstr, method.Parameters[i].DefaultParameterType.DefaultFullName));
                                instructions.Add(new Instruction(CilOpCodes.Stelem_Ref));
                            }

                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppMethod));
                        }
                        else
                        {
                            instructions.Add(new Instruction(CilOpCodes.Call, getClassPointerMethodInstantiated));
                            instructions.Add(new Instruction(CilOpCodes.Ldc_I4, unchecked((int)method.Token)));
                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppMethodByToken));
                        }
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, concreteMethodInfoStore));

                        if (method.GenericParameters.Count > 0)
                        {
                            var methodInfoPtrGenericClass = initializationType.InjectNestedType(
                                $"MethodInfoPtrGeneric_{index}",
                                appContext.SystemTypes.SystemObjectType,
                                TypeAttributes.NestedAssembly | TypeAttributes.Abstract | TypeAttributes.Sealed);
                            methodInfoPtrGenericClass.IsInjected = true;
                            methodInfoPtrGenericClass.CopyGenericParameters(initializationType, false, true);
                            methodInfoPtrGenericClass.CopyGenericParameters(method, false, true);
                            methodInfoPtrGenericClass.GenericParameters.CopyConstraintsFrom([.. initializationType.GenericParameters, .. method.GenericParameters]);

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
                            instructions2.Add(new Instruction(CilOpCodes.Ldsfld, concreteMethodInfoStore2));
                            instructions2.Add(new Instruction(CilOpCodes.Call, getClassPointerMethodInstantiated));
                            instructions2.Add(new Instruction(CilOpCodes.Ldc_I4, method.GenericParameters.Count));
                            instructions2.Add(new Instruction(CilOpCodes.Newarr, appContext.SystemTypes.SystemIntPtrType));
                            for (var j = 0; j < method.GenericParameters.Count; j++)
                            {
                                var genericParameter = methodInfoPtrGenericClass.GenericParameters[j + initializationType.GenericParameters.Count];
                                instructions2.Add(new Instruction(CilOpCodes.Dup));
                                instructions2.Add(new Instruction(CilOpCodes.Ldc_I4, j));
                                instructions2.Add(new Instruction(CilOpCodes.Call, il2CppType_GetClassPointer.MakeGenericInstanceMethod(genericParameter)));
                                instructions2.Add(new Instruction(CilOpCodes.Stelem_I));
                            }
                            instructions2.Add(new Instruction(CilOpCodes.Call, getIl2CppGenericInstanceMethod));
                            instructions2.Add(new Instruction(CilOpCodes.Stsfld, new ConcreteGenericFieldAnalysisContext(methodInfoPtrGenericField, methodInfoPtrGenericClass.MakeGenericInstanceType(methodInfoPtrGenericClass.GenericParameters))));
                            instructions2.Add(new Instruction(CilOpCodes.Ret));

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
                        {
                            delegateType = initializationType.InjectNestedType(
                                $"ICall_Delegate_Type_{index}",
                                multicastDelegateType);

                            var returnType = method.ReturnType;
                            IEnumerable<TypeAnalysisContext> parameterTypes;
                            IEnumerable<string> parameterNames;
                            if (method.IsStatic)
                            {
                                parameterTypes = method.Parameters.Select(p => p.ParameterType);
                                parameterNames = Enumerable.Range(0, method.Parameters.Count).Select(i => $"param_{i}");
                            }
                            else
                            {
                                var thisParameterType = type.IsValueType ? byReference.MakeGenericInstanceType([type]) : type;
                                parameterTypes = method.Parameters.Select(p => p.ParameterType).Prepend(thisParameterType);
                                parameterNames = Enumerable.Range(0, method.Parameters.Count).Select(i => $"param_{i}").Prepend("this");
                            }

                            // Constructor
                            {
                                delegateType.Methods.Add(new InjectedMethodAnalysisContext(
                                    delegateType,
                                    ".ctor",
                                    appContext.SystemTypes.SystemVoidType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                                    [appContext.SystemTypes.SystemObjectType, appContext.SystemTypes.SystemIntPtrType],
                                    defaultImplAttributes: MethodImplAttributes.Runtime));
                            }

                            // Invoke
                            {
                                delegateType.Methods.Add(new InjectedMethodAnalysisContext(
                                    delegateType,
                                    "Invoke",
                                    returnType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                                    parameterTypes.ToArray(),
                                    parameterNames.ToArray(),
                                    defaultImplAttributes: MethodImplAttributes.Runtime));
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
                                    defaultImplAttributes: MethodImplAttributes.Runtime));
                            }

                            // EndInvoke
                            {
                                delegateType.Methods.Add(new InjectedMethodAnalysisContext(
                                    delegateType,
                                    "EndInvoke",
                                    returnType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                                    [iasyncResultType],
                                    ["result"],
                                    defaultImplAttributes: MethodImplAttributes.Runtime));
                            }
                        }

                        // ICall_Delegate_Field_0
                        FieldAnalysisContext delegateField;
                        {
                            delegateField = initializationType.InjectFieldContext(
                                $"ICall_Delegate_Field_{index}",
                                delegateType,
                                FieldAttributes.Assembly | FieldAttributes.Static | FieldAttributes.InitOnly);

                            method.ICallDelegateField = delegateField;
                        }

                        // Static constructor instructions
                        {
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, $"{type.DefaultFullName}::{method.DefaultName}"));
                            instructions.Add(new Instruction(CilOpCodes.Call, new ConcreteGenericMethodAnalysisContext(resolveICall, [], [delegateType])));
                            instructions.Add(new Instruction(CilOpCodes.Stsfld, delegateField));
                        }
                    }

                    instructions.Add(new Instruction(CilOpCodes.Ret));

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
            foreach (var typeToResolve in appContext.Binary.AllTypes)
            {
                var typeContext = appContext.ResolveIl2CppType(typeToResolve);

                if (typeContext is not ReferencedTypeAnalysisContext && typeContext.GenericParameters.Count > 0)
                    continue; // Skip open generics

                if (InvalidTypeChecker.ContainsInvalidType(typeContext))
                    continue;

                typeContext = typeConverter.Replace(typeContext);

                // Must happen after type conversion
                if (InvalidConstraintChecker.ContainsInvalidConstraint(typeContext))
                    continue;

                if (!processedTypes.Add(typeContext))
                    continue;

                // Ensure the type is initialized
                instructions.Add(new Instruction(CilOpCodes.Ldtoken, typeContext));
                instructions.Add(new Instruction(CilOpCodes.Call, runClassConstructor));
            }

            instructions.Add(new Instruction(CilOpCodes.Ret));

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

        instructions.Add(new Instruction(CilOpCodes.Ldtoken, typeToInitialize));
        instructions.Add(new Instruction(CilOpCodes.Call, runClassConstructor));
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

    /// <summary>
    /// Unity uses System.Object as a type argument when it deduplicates generic instantiations.
    /// However, that might violate the constraints, so we need to detect those cases and skip generating initialization code for them.
    /// </summary>
    /// <remarks>
    /// This only checks for IObject because Object should have already been replaced.<br/>
    ///
    /// The fact that we need to check for these invalid constraints could indicate a major issue in the generated code. Either:<br/>
    /// * The other instantiations don't exist in the Il2Cpp runtime (calling GetType() returns a type with object as the type argument).
    ///   This would mean the types are unusable unless we remove the constraints.<br/>
    /// * The other instantiations do exist in the Il2Cpp runtime, and we need to use Il2Cpp reflection to construct the .NET Core type.
    ///   That is how System.__Canon works in .NET Core.
    /// </remarks>
    private sealed class InvalidConstraintChecker : BooleanOrTypeVisitor
    {
        public static InvalidConstraintChecker Instance { get; } = new InvalidConstraintChecker();
        public static bool ContainsInvalidConstraint(TypeAnalysisContext type)
        {
            return Instance.Visit(type);
        }

        public override bool Visit(GenericInstanceTypeAnalysisContext type)
        {
            for (var i = 0; i < type.GenericArguments.Count; i++)
            {
                if (!IsObject(type.GenericArguments[i]))
                    continue;

                // Check if any of the constraints on this generic parameter are not satisfied by object.
                foreach (var constraint in type.GenericType.GenericParameters[i].ConstraintTypes)
                {
                    if (IsObject(constraint))
                        continue;
                    if (!IsInjectedOrReference(constraint))
                        return true;
                }

                // Check if any of the constraints on any of the declaring types' generic parameters might not be satisfied by object.
                // Specifically, we check if a constraint references the target generic parameter.
                // Obviously, this could have false positives, but it shouldn't have any false negatives.
                var visitor = new TargetTypeFinder(type.GenericType.GenericParameters[i]);
                for (var j = 0; j < type.GenericType.GenericParameters.Count; j++)
                {
                    if (j == i)
                        continue;
                    foreach (var constraint in type.GenericType.GenericParameters[j].ConstraintTypes)
                    {
                        if (visitor.Visit(constraint))
                            return true;
                    }
                }
            }
            return base.Visit(type);
        }

        private static bool IsInjectedOrReference(TypeAnalysisContext type)
        {
            type = (type as GenericInstanceTypeAnalysisContext)?.GenericType ?? type;

            return type.IsInjected || type.DeclaringAssembly.IsReferenceAssembly;
        }

        private static bool IsObject(TypeAnalysisContext type)
        {
            return type.KnownType is KnownTypeCode.Il2CppSystem_IObject;
        }

        private sealed class TargetTypeFinder(GenericParameterTypeAnalysisContext targetType) : BooleanOrTypeVisitor
        {
            public override bool Visit(GenericParameterTypeAnalysisContext type) => type == targetType;
        }
    }
}
