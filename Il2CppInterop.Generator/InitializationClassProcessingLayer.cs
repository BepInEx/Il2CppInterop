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
using Il2CppInterop.Generator.Visitors;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Runtime;

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

        var classInjector = appContext.ResolveTypeOrThrow(typeof(ClassInjector));
        var registerTypeInIl2Cpp = classInjector.Methods.Single(m =>
        {
            return m.Name == nameof(ClassInjector.RegisterTypeInIl2Cpp) && m.Parameters.Count is 0 && m.GenericParameters.Count == 1;
        });

        var multicastDelegateType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.MulticastDelegate");
        var asyncCallbackType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.AsyncCallback");
        var iasyncResultType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.IAsyncResult");

        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_CopyFrom = byReference.GetMethodByName(nameof(ByReference<>.CopyFrom));
        var byReference_CopyTo = byReference.GetMethodByName(nameof(ByReference<>.CopyTo));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");

        var il2CppTypeHelper_SizeOf = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper)).GetMethodByName(nameof(Il2CppTypeHelper.SizeOf));

        var il2CppObjectPool = appContext.ResolveTypeOrThrow(typeof(Il2CppObjectPool));
        var il2CppObjectPool_RegisterInitializer = il2CppObjectPool.GetMethodByName(nameof(Il2CppObjectPool.RegisterInitializer));
        var il2CppObjectPool_ValueTypeInitializer = il2CppObjectPool.GetMethodByName(nameof(Il2CppObjectPool.ValueTypeInitializer));

        var funcTypeInstantiated = (GenericInstanceTypeAnalysisContext)il2CppObjectPool_RegisterInitializer.Parameters[1].ParameterType;
        var funcType = funcTypeInstantiated.GenericType;
        var funcConstructor = funcType.Methods.Single(m => m.IsInstanceConstructor && m.Parameters.Count == 2);
        var funcConstructorInstantiated = funcConstructor.MakeConcreteGeneric(funcTypeInstantiated.GenericArguments, []);

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

                var initializationType = assembly.InjectType(
                    "Il2CppInterop.Generated",
                    $"Il2CppInternals_{HashString(type.FullName):x16}",
                    appContext.SystemTypes.SystemObjectType,
                    TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class);
                initializationType.IsInjected = true;
                initializationType.CopyGenericParameters(type, true, true);

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

                            // Il2CppClassPointerStore<NestedClass>.NativeClassPtr = IL2CPP.GetIl2CppNestedType(Il2CppClassPointerStore<DeclaringType>.NativeClassPtr, "NestedClass");
                            var declaringTypeClassPointerField = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([typeToInitialize.DeclaringType]));
                            instructions.Add(new Instruction(CilOpCodes.Ldsfld, declaringTypeClassPointerField));
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, type.DefaultName));// typeToInitialize can have the wrong DefaultName
                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppNestedType));
                        }
                        else
                        {
                            // Il2CppClassPointerStore<Class>.NativeClassPtr = IL2CPP.GetIl2CppClass("Assembly-CSharp.dll", "", "Class");
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, $"{assembly.DefaultName}.dll"));
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
                                var genericParameter = initializationType.GenericParameters[j];
                                var classPointerForGenericParameter = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([genericParameter]));
                                instructions.Add(new Instruction(CilOpCodes.Ldsfld, classPointerForGenericParameter));
                                instructions.Add(new Instruction(CilOpCodes.Stelem_I));
                            }
                            instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppGenericInstanceType));
                        }
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, concreteClassPointerField));
                    }

                    // IL2CPP.il2cpp_runtime_class_init(Il2CppClassPointerStore<Class>.NativeClassPtr);
                    instructions.Add(new Instruction(CilOpCodes.Ldsfld, concreteClassPointerField));
                    instructions.Add(new Instruction(CilOpCodes.Call, il2CppRuntimeClassInit));

                    // Size = IL2CPP.il2cpp_class_value_size(Il2CppClassPointerStore<Class>.NativeClassPtr, ref align);
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

                        instructions.Add(new Instruction(CilOpCodes.Ldsfld, concreteClassPointerField));
                        instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppValueSize));
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, instantiatedSizeStore));
                    }

                    // FieldOffset_0 = (int)IL2CPP.il2cpp_field_get_offset(IL2CPP.GetIl2CppField(Il2CppClassPointerStore<Class>.NativeClassPtr, "field_name"));
                    for (var index = 0; index < type.Fields.Count; index++)
                    {
                        var field = type.Fields[index];

                        if (field.IsInjected)
                            continue;

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

                        instructions.Add(new Instruction(CilOpCodes.Ldsfld, concreteClassPointerField));
                        instructions.Add(new Instruction(CilOpCodes.Ldstr, field.DefaultName));
                        instructions.Add(new Instruction(CilOpCodes.Call, getIl2CppField));
                        instructions.Add(new Instruction(CilOpCodes.Dup));
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, instantiatedInfoStore));
                        instructions.Add(new Instruction(CilOpCodes.Call, il2CppFieldGetOffset));
                        instructions.Add(new Instruction(CilOpCodes.Conv_I4));
                        instructions.Add(new Instruction(CilOpCodes.Stsfld, instantiatedOffsetStore));
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

                            instructions.Add(new Instruction(CilOpCodes.Ldsfld, concreteClassPointerField));
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
                            instructions.Add(new Instruction(CilOpCodes.Ldsfld, concreteClassPointerField));
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
                            instructions2.Add(new Instruction(CilOpCodes.Ldsfld, concreteClassPointerField));
                            instructions2.Add(new Instruction(CilOpCodes.Ldc_I4, method.GenericParameters.Count));
                            instructions2.Add(new Instruction(CilOpCodes.Newarr, appContext.SystemTypes.SystemIntPtrType));
                            for (var j = 0; j < method.GenericParameters.Count; j++)
                            {
                                instructions2.Add(new Instruction(CilOpCodes.Dup));
                                instructions2.Add(new Instruction(CilOpCodes.Ldc_I4, j));
                                var genericParameter = methodInfoPtrGenericClass.GenericParameters[j + initializationType.GenericParameters.Count];
                                var classPointerForGenericParameter = new ConcreteGenericFieldAnalysisContext(classPointerField, il2CppClassPointerStore.MakeGenericInstanceType([genericParameter]));
                                instructions2.Add(new Instruction(CilOpCodes.Ldsfld, classPointerForGenericParameter));
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
                        MethodAnalysisContext invokeMethod;
                        {
                            delegateType = initializationType.InjectNestedType(
                                $"ICall_Delegate_Type_{index}",
                                multicastDelegateType);

                            TypeAnalysisContext returnType = method.ReturnType;
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
                                invokeMethod = new InjectedMethodAnalysisContext(
                                    delegateType,
                                    "Invoke",
                                    returnType,
                                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                                    parameterTypes.ToArray(),
                                    parameterNames.ToArray(),
                                    defaultImplAttributes: MethodImplAttributes.Runtime);
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
                        }

                        // Method body
                        {
                            List<Instruction> methodInstructions = [];

                            LocalVariable? thisLocal;
                            if (method.IsStatic)
                            {
                                thisLocal = null;
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldsfld, delegateField));
                            }
                            else if (type.IsValueType)
                            {
                                thisLocal = new LocalVariable(byReference.MakeGenericInstanceType([type]));

                                methodInstructions.Add(new Instruction(CilOpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(type)));
                                methodInstructions.Add(new Instruction(CilOpCodes.Conv_U));
                                methodInstructions.Add(new Instruction(CilOpCodes.Localloc));
                                methodInstructions.Add(new Instruction(CilOpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([type], [])));
                                methodInstructions.Add(new Instruction(CilOpCodes.Stloc, thisLocal));

                                methodInstructions.Add(new Instruction(CilOpCodes.Ldloca, thisLocal));
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldarg, This.Instance));
                                methodInstructions.Add(new Instruction(CilOpCodes.Call, byReference_CopyFrom.MakeConcreteGeneric([type], [])));

                                methodInstructions.Add(new Instruction(CilOpCodes.Ldsfld, delegateField));
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldloc, thisLocal));
                            }
                            else
                            {
                                thisLocal = null; // Not needed for reference types
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldsfld, delegateField));
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldarg, This.Instance));
                            }

                            foreach (var parameter in method.Parameters)
                            {
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldarg, parameter));
                            }
                            methodInstructions.Add(new Instruction(CilOpCodes.Callvirt, invokeMethod));

                            if (thisLocal is not null)
                            {
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldloca, thisLocal));
                                methodInstructions.Add(new Instruction(CilOpCodes.Ldarg, This.Instance));
                                methodInstructions.Add(new Instruction(CilOpCodes.Call, byReference_CopyTo.MakeConcreteGeneric([type], [])));
                            }

                            methodInstructions.Add(new Instruction(CilOpCodes.Ret));

                            method.PutExtraData(new NativeMethodBody()
                            {
                                Instructions = methodInstructions,
                                LocalVariables = thisLocal is not null ? [thisLocal] : [],
                            });
                        }

                        // Static constructor instructions
                        {
                            instructions.Add(new Instruction(CilOpCodes.Ldstr, $"{type.DefaultFullName}::{method.DefaultName}"));
                            instructions.Add(new Instruction(CilOpCodes.Call, new ConcreteGenericMethodAnalysisContext(resolveICall, [], [delegateType])));
                            instructions.Add(new Instruction(CilOpCodes.Stsfld, delegateField));
                        }
                    }

                    // Il2CppObjectPool.RegisterInitialier
                    {
                        if (type.IsAbstract || type.IsInterface)
                        {
                            // Cannot register initializers for abstract types or interfaces
                        }
                        else if (type.IsValueType)
                        {
                            instructions.Add(CilOpCodes.Ldsfld, concreteClassPointerField);
                            instructions.Add(CilOpCodes.Ldnull);
                            instructions.Add(CilOpCodes.Ldftn, il2CppObjectPool_ValueTypeInitializer.MakeGenericInstanceMethod(typeToInitialize));
                            instructions.Add(CilOpCodes.Newobj, funcConstructorInstantiated);
                            instructions.Add(CilOpCodes.Call, il2CppObjectPool_RegisterInitializer);
                        }
                        else
                        {
                            var creationMethod = new InjectedMethodAnalysisContext(
                                initializationType,
                                "Create",
                                funcTypeInstantiated.GenericArguments[1],
                                MethodAttributes.Private | MethodAttributes.Static | MethodAttributes.HideBySig,
                                [funcTypeInstantiated.GenericArguments[0]]);
                            initializationType.Methods.Add(creationMethod);

                            var pointerConstructor = type.PointerConstructor;
                            Debug.Assert(pointerConstructor is not null);

                            creationMethod.PutExtraData(new NativeMethodBody()
                            {
                                Instructions =
                                [
                                    new Instruction(CilOpCodes.Ldarg_0),
                                    new Instruction(CilOpCodes.Newobj, pointerConstructor.MaybeMakeConcreteGeneric(initializationType.GenericParameters, [])),
                                    new Instruction(CilOpCodes.Ret),
                                ]
                            });

                            instructions.Add(CilOpCodes.Ldsfld, concreteClassPointerField);
                            instructions.Add(CilOpCodes.Ldnull);
                            instructions.Add(CilOpCodes.Ldftn, creationMethod.MaybeMakeConcreteGeneric(initializationType.GenericParameters, []));
                            instructions.Add(CilOpCodes.Newobj, funcConstructorInstantiated);
                            instructions.Add(CilOpCodes.Call, il2CppObjectPool_RegisterInitializer);
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
}
