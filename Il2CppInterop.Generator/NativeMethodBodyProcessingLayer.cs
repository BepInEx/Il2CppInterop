using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class NativeMethodBodyProcessingLayer : Cpp2IlProcessingLayer
{
    private const int ParameterOffset = 2;
    public override string Name => "Native Method Body Processor";
    public override string Id => "native_method_body_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var iil2CppTypeGeneric = appContext.ResolveTypeOrThrow(typeof(IIl2CppType<>));

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper));
        var il2CppTypeHelper_SizeOf = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.SizeOf));
        var il2CppTypeHelper_ReadFromPointer = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadFromPointer));
        var il2CppTypeHelper_WriteToPointer = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteToPointer));

        var runtimeInvokeHelper = appContext.ResolveTypeOrThrow(typeof(RuntimeInvokeHelper));
        var invokeAction = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.InvokeAction));
        var invokeFunction = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.InvokeFunction));
        var requiredStackAllocationSize = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.RequiredStackAllocationSize));
        var prepareParameter = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.PrepareParameter));
        var cleanupParameter = runtimeInvokeHelper.GetMethodByName(nameof(RuntimeInvokeHelper.CleanupParameter));

        var intptrPointerType = appContext.SystemTypes.SystemIntPtrType.MakePointerType();
        var bytePointerType = appContext.SystemTypes.SystemByteType.MakePointerType();

        var byRefType = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var fromRef = byRefType.GetMethodByName(nameof(ByReference<>.FromRef));

        var iil2CppObjectBase = appContext.ResolveTypeOrThrow(typeof(IIl2CppObjectBase));
        var get_Pointer = iil2CppObjectBase.GetMethodByName($"get_{nameof(IIl2CppObjectBase.Pointer)}");

        var objectPointerType = appContext.ResolveTypeOrThrow(typeof(ObjectPointer));

        var objectPointerNew = appContext.ResolveTypeOrThrow(typeof(IL2CPP)).GetMethodByName(nameof(IL2CPP.NewObjectPointer));

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            var assemblyInvokeHelper = assembly.InjectType(
                "Il2CppInterop.Generated.Helpers",
                "InvokeHelper",
                appContext.SystemTypes.SystemObjectType,
                TypeAttributes.NotPublic | TypeAttributes.Abstract | TypeAttributes.Sealed);
            assemblyInvokeHelper.IsInjected = true;

            var actionDictionary = new Dictionary<int, InjectedMethodAnalysisContext>();
            var functionDictionary = new Dictionary<int, InjectedMethodAnalysisContext>();

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected || type.IsUnstripped)
                    continue;
                foreach (var method in type.Methods)
                {
                    if (method.IsInjected || method.IsUnstripped)
                        continue;

                    if (method.HasExtraData<TranslatedMethodBody>())
                        continue; // Already has a translated body, skip.

                    Debug.Assert(!method.HasExtraData<NativeMethodBody>());

                    var parameterCount = method.Parameters.Count;
                    MethodAnalysisContext invokeHelper;
                    if (method.IsVoid)
                    {
                        if (!actionDictionary.TryGetValue(parameterCount, out var actionMethod))
                        {
                            actionMethod = new InjectedMethodAnalysisContext(
                                assemblyInvokeHelper,
                                "InvokeAction",
                                appContext.SystemTypes.SystemVoidType,
                                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                                [])
                            {
                                IsInjected = true,
                            };

                            AddParameterForMethodInfo(actionMethod);
                            AddParameterForObjectPointer(actionMethod);
                            AddNormalAndGenericParameters(actionMethod, parameterCount, iil2CppTypeGeneric);

                            actionDictionary[parameterCount] = actionMethod;

                            var instructions = new List<Instruction>();
                            var localVariables = new List<LocalVariable>();

                            if (parameterCount == 0)
                            {
                                AndImplementationForParameterlessMethod(instructions, invokeAction);
                            }
                            else
                            {
                                var parametersPointerLocal = StackAllocateIntPtr(instructions, localVariables, parameterCount, intptrPointerType, appContext);

                                var stackAllocDataLocals = new LocalVariable[parameterCount];
                                for (var i = 0; i < parameterCount; i++)
                                {
                                    var parameter = actionMethod.Parameters[i + ParameterOffset];
                                    var stackAllocSizeLocal = localVariables.AddNew(appContext.SystemTypes.SystemInt32Type);
                                    var stackAllocDataLocal = localVariables.AddNew(bytePointerType);

                                    StackAllocateForParameter(instructions, parameter, stackAllocSizeLocal, stackAllocDataLocal, requiredStackAllocationSize);

                                    // @params[{i}] =
                                    instructions.Add(OpCodes.Ldloc, parametersPointerLocal);
                                    AddOffsetForPointerIndex(instructions, i, appContext);

                                    // PrepareParameter(parameter, stackAllocData)
                                    PrepareParameter(instructions, parameter, stackAllocDataLocal, prepareParameter);

                                    // Write the parameter to the pointer
                                    instructions.Add(OpCodes.Stind_I);

                                    stackAllocDataLocals[i] = stackAllocDataLocal;
                                }

                                // InvokeAction(method, obj, (void**)parameters);
                                InvokeMethod(instructions, parametersPointerLocal, invokeAction);

                                for (var i = 0; i < parameterCount; i++)
                                {
                                    // CleanupParameter(parameter, stackAllocData)
                                    CleanupParameter(instructions, actionMethod.Parameters[i + ParameterOffset], stackAllocDataLocals[i], cleanupParameter);
                                }

                                instructions.Add(OpCodes.Ret);
                            }

                            actionMethod.PutExtraData(new NativeMethodBody()
                            {
                                Instructions = instructions,
                                LocalVariables = localVariables.Count > 0 ? localVariables : [],
                            });
                        }
                        invokeHelper = actionMethod;
                    }
                    else
                    {
                        if (!functionDictionary.TryGetValue(method.Parameters.Count, out var functionMethod))
                        {
                            functionMethod = new InjectedMethodAnalysisContext(
                                assemblyInvokeHelper,
                                "InvokeFunction",
                                appContext.SystemTypes.SystemVoidType,
                                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                                [])
                            {
                                IsInjected = true,
                            };

                            AddParameterForMethodInfo(functionMethod);
                            AddParameterForObjectPointer(functionMethod);
                            AddNormalAndGenericParameters(functionMethod, parameterCount, iil2CppTypeGeneric);
                            AddReturnGenericParameterAndSetReturnType(functionMethod, iil2CppTypeGeneric);

                            functionDictionary[method.Parameters.Count] = functionMethod;

                            var instructions = new List<Instruction>();
                            var localVariables = new List<LocalVariable>();

                            var invokeFunctionInstantiated = invokeFunction.MakeGenericInstanceMethod(functionMethod.GenericParameters[^1]);

                            if (parameterCount == 0)
                            {
                                AndImplementationForParameterlessMethod(instructions, invokeFunctionInstantiated);
                            }
                            else
                            {
                                var parametersPointerLocal = StackAllocateIntPtr(instructions, localVariables, parameterCount, intptrPointerType, appContext);

                                var stackAllocDataLocals = new LocalVariable[parameterCount];
                                for (var i = 0; i < parameterCount; i++)
                                {
                                    var parameter = functionMethod.Parameters[i + ParameterOffset];
                                    var stackAllocSizeLocal = localVariables.AddNew(appContext.SystemTypes.SystemInt32Type);
                                    var stackAllocDataLocal = localVariables.AddNew(bytePointerType);

                                    StackAllocateForParameter(instructions, parameter, stackAllocSizeLocal, stackAllocDataLocal, requiredStackAllocationSize);

                                    // @params[{i}] =
                                    instructions.Add(OpCodes.Ldloc, parametersPointerLocal);
                                    AddOffsetForPointerIndex(instructions, i, appContext);

                                    // PrepareParameter(parameter, stackAllocData)
                                    PrepareParameter(instructions, parameter, stackAllocDataLocal, prepareParameter);

                                    // Write the parameter to the pointer
                                    instructions.Add(OpCodes.Stind_I);

                                    stackAllocDataLocals[i] = stackAllocDataLocal;
                                }

                                // var result = InvokeFunction(method, obj, (void**)parameters);
                                var resultLocal = localVariables.AddNew(functionMethod.ReturnType);
                                InvokeMethod(instructions, parametersPointerLocal, invokeFunctionInstantiated);
                                instructions.Add(OpCodes.Stloc, resultLocal);

                                for (var i = 0; i < parameterCount; i++)
                                {
                                    // CleanupParameter(parameter, stackAllocData)
                                    CleanupParameter(instructions, functionMethod.Parameters[i + ParameterOffset], stackAllocDataLocals[i], cleanupParameter);
                                }

                                instructions.Add(OpCodes.Ldloc, resultLocal);
                                instructions.Add(OpCodes.Ret);
                            }

                            functionMethod.PutExtraData(new NativeMethodBody()
                            {
                                Instructions = instructions,
                                LocalVariables = localVariables.Count > 0 ? localVariables : [],
                            });
                        }
                        invokeHelper = functionMethod;
                    }

                    // Implement the method body
                    {
                        var instructions = new List<Instruction>();

                        Debug.Assert(method.DeclaringType is not null);

                        if (method.IsInstanceConstructor && !method.DeclaringType.IsValueType)
                        {
                            Debug.Assert(method.DeclaringType.PointerConstructor is not null);

                            var pointerConstructorInstantiated = method.DeclaringType.GenericParameters.Count > 0
                                ? new ConcreteGenericMethodAnalysisContext(method.DeclaringType.PointerConstructor!, method.DeclaringType.GenericParameters, [])
                                : method.DeclaringType.PointerConstructor!;

                            instructions.Add(OpCodes.Ldarg_0);
                            instructions.Add(OpCodes.Call, objectPointerNew.MakeGenericInstanceMethod(method.DeclaringType.SelfInstantiateIfGeneric()));
                            instructions.Add(OpCodes.Call, pointerConstructorInstantiated);
                        }

                        // MethodInfo and Object Pointer
                        LocalVariable? valueTypeDataPointer = null;
                        if (method.IsStatic)
                        {
                            instructions.Add(OpCodes.Ldsfld, method.GetInstantiatedMethodInfoField());
                            instructions.Add(OpCodes.Ldc_I4_0);
                            instructions.Add(OpCodes.Conv_I);
                        }
                        else if (method.DeclaringType.IsValueType)
                        {
                            valueTypeDataPointer = new LocalVariable(bytePointerType);
                            var declaringTypeInstantiated = method.DeclaringType.SelfInstantiateIfGeneric();

                            instructions.Add(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod([declaringTypeInstantiated]));
                            instructions.Add(OpCodes.Conv_U);
                            instructions.Add(OpCodes.Localloc);
                            instructions.Add(OpCodes.Stloc, valueTypeDataPointer);

                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Ldobj, declaringTypeInstantiated);
                            instructions.Add(OpCodes.Ldloc, valueTypeDataPointer);
                            instructions.Add(OpCodes.Call, il2CppTypeHelper_WriteToPointer.MakeGenericInstanceMethod(declaringTypeInstantiated));

                            instructions.Add(OpCodes.Ldsfld, method.GetInstantiatedMethodInfoField());
                            instructions.Add(OpCodes.Ldloc, valueTypeDataPointer);
                        }
                        else
                        {
                            instructions.Add(OpCodes.Ldsfld, method.GetInstantiatedMethodInfoField());
                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Callvirt, get_Pointer);
                        }

                        var genericArgumentsCount = method.IsVoid ? method.Parameters.Count : method.Parameters.Count + 1;
                        var genericArguments = new TypeAnalysisContext[genericArgumentsCount];
                        for (var i = 0; i < method.Parameters.Count; i++)
                        {
                            var parameter = method.Parameters[i];
                            var parameterType = parameter.ParameterType;

                            instructions.Add(OpCodes.Ldarg, parameter);
                            if (parameterType is ByRefTypeAnalysisContext byRefTypeAnalysisContext)
                            {
                                instructions.Add(OpCodes.Call, new ConcreteGenericMethodAnalysisContext(fromRef, [byRefTypeAnalysisContext.ElementType], []));
                                genericArguments[i] = byRefType.MakeGenericInstanceType([byRefTypeAnalysisContext.ElementType]);
                            }
                            else
                            {
                                genericArguments[i] = parameterType;
                            }
                        }
                        if (!method.IsVoid)
                        {
                            genericArguments[^1] = method.ReturnType;
                        }
                        var invokeHelperInstantiated = genericArguments.Length > 0
                            ? invokeHelper.MakeGenericInstanceMethod(genericArguments)
                            : invokeHelper;
                        instructions.Add(OpCodes.Call, invokeHelperInstantiated);

                        if (valueTypeDataPointer is not null)
                        {
                            var declaringTypeInstantiated = method.DeclaringType.SelfInstantiateIfGeneric();

                            instructions.Add(OpCodes.Ldarg, This.Instance);
                            instructions.Add(OpCodes.Ldloc, valueTypeDataPointer);
                            instructions.Add(OpCodes.Call, il2CppTypeHelper_ReadFromPointer.MakeGenericInstanceMethod(declaringTypeInstantiated));
                            instructions.Add(OpCodes.Stobj, declaringTypeInstantiated);
                        }

                        instructions.Add(OpCodes.Ret);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = valueTypeDataPointer is not null
                                ? [valueTypeDataPointer]
                                : [],
                        });
                    }
                }
            }

            foreach (var method in actionDictionary.OrderBy(pair => pair.Key).Select(pair => pair.Value))
            {
                assemblyInvokeHelper.Methods.Add(method);
            }
            foreach (var method in functionDictionary.OrderBy(pair => pair.Key).Select(pair => pair.Value))
            {
                assemblyInvokeHelper.Methods.Add(method);
            }
        }
    }

    private static void InvokeMethod(List<Instruction> instructions, LocalVariable parametersPointerLocal, MethodAnalysisContext invokeHelper)
    {
        instructions.Add(OpCodes.Ldarg_0); // method
        instructions.Add(OpCodes.Ldarg_1); // obj
        instructions.Add(OpCodes.Ldloc, parametersPointerLocal); // parameters
        instructions.Add(OpCodes.Call, invokeHelper);
    }

    private static void CleanupParameter(List<Instruction> instructions, ParameterAnalysisContext parameter, LocalVariable stackAllocDataLocal, MethodAnalysisContext cleanupParameter)
    {
        instructions.Add(OpCodes.Ldarg, parameter);
        instructions.Add(OpCodes.Ldloc, stackAllocDataLocal);
        instructions.Add(OpCodes.Call, cleanupParameter.MakeGenericInstanceMethod(parameter.ParameterType));
    }

    private static void PrepareParameter(List<Instruction> instructions, ParameterAnalysisContext parameter, LocalVariable stackAllocDataLocal, MethodAnalysisContext prepareParameter)
    {
        instructions.Add(OpCodes.Ldarg, parameter);
        instructions.Add(OpCodes.Ldloc, stackAllocDataLocal);
        instructions.Add(OpCodes.Call, prepareParameter.MakeGenericInstanceMethod(parameter.ParameterType));
    }

    private static void StackAllocateForParameter(List<Instruction> instructions, ParameterAnalysisContext parameter, LocalVariable stackAllocSizeLocal, LocalVariable stackAllocDataLocal, MethodAnalysisContext requiredStackAllocationSize)
    {
        instructions.Add(OpCodes.Call, requiredStackAllocationSize.MakeGenericInstanceMethod(parameter.ParameterType));
        instructions.Add(OpCodes.Stloc, stackAllocSizeLocal);

        var zeroSizeLabel = new Instruction(OpCodes.Nop);
        var endLabel = new Instruction(OpCodes.Nop);

        instructions.Add(OpCodes.Ldloc, stackAllocSizeLocal);
        instructions.Add(OpCodes.Brfalse, zeroSizeLabel);

        instructions.Add(OpCodes.Ldloc, stackAllocSizeLocal);
        instructions.Add(OpCodes.Conv_U);
        instructions.Add(OpCodes.Localloc);
        instructions.Add(OpCodes.Stloc, stackAllocDataLocal);
        instructions.Add(OpCodes.Br, endLabel);

        instructions.Add(zeroSizeLabel);
        instructions.Add(OpCodes.Ldc_I4_0);
        instructions.Add(OpCodes.Conv_U);
        instructions.Add(OpCodes.Stloc, stackAllocDataLocal);

        instructions.Add(endLabel);
    }

    private static void AddOffsetForPointerIndex(List<Instruction> instructions, int index, ApplicationAnalysisContext appContext)
    {
        if (index != 0)
        {
            if (index > 1)
            {
                instructions.Add(OpCodes.Ldc_I4, index);
                instructions.Add(OpCodes.Conv_I);
            }

            instructions.Add(OpCodes.Sizeof, appContext.SystemTypes.SystemIntPtrType);

            if (index > 1)
            {
                instructions.Add(OpCodes.Mul);
            }

            instructions.Add(OpCodes.Add);
        }
    }

    private static LocalVariable StackAllocateIntPtr(List<Instruction> instructions, List<LocalVariable> localVariables, int count, PointerTypeAnalysisContext intptrPointerType, ApplicationAnalysisContext appContext)
    {
        var parametersPointerLocal = localVariables.AddNew(intptrPointerType);

        instructions.Add(OpCodes.Ldc_I4, count);
        instructions.Add(OpCodes.Conv_U);
        instructions.Add(OpCodes.Sizeof, appContext.SystemTypes.SystemIntPtrType);
        instructions.Add(OpCodes.Mul_Ovf_Un);
        instructions.Add(OpCodes.Localloc);
        instructions.Add(OpCodes.Stloc, parametersPointerLocal);
        return parametersPointerLocal;
    }

    private static void AndImplementationForParameterlessMethod(List<Instruction> instructions, MethodAnalysisContext invokeHelper)
    {
        instructions.Add(OpCodes.Ldarg_0);
        instructions.Add(OpCodes.Ldarg_1);
        instructions.Add(OpCodes.Ldc_I4_0);
        instructions.Add(OpCodes.Conv_U);
        instructions.Add(OpCodes.Call, invokeHelper);
        instructions.Add(OpCodes.Ret);
    }

    private static void AddParameterForMethodInfo(InjectedMethodAnalysisContext method)
    {
        method.Parameters.Add(new InjectedParameterAnalysisContext(
            "method",
            method.AppContext.SystemTypes.SystemIntPtrType,
            ParameterAttributes.None,
            0,
            method));
    }

    private static void AddParameterForObjectPointer(InjectedMethodAnalysisContext method)
    {
        method.Parameters.Add(new InjectedParameterAnalysisContext(
            "obj",
            method.AppContext.SystemTypes.SystemIntPtrType,
            ParameterAttributes.None,
            1,
            method));
    }

    private static void AddNormalAndGenericParameters(InjectedMethodAnalysisContext method, int parameterCount, TypeAnalysisContext iil2CppTypeGeneric)
    {
        for (var i = 0; i < parameterCount; i++)
        {
            var genericParameter = new GenericParameterTypeAnalysisContext(
                "T" + i,
                i,
                LibCpp2IL.BinaryStructures.Il2CppTypeEnum.IL2CPP_TYPE_MVAR,
                GenericParameterAttributes.None,
                method);
            method.GenericParameters.Add(genericParameter);

            genericParameter.ConstraintTypes.Add(iil2CppTypeGeneric.MakeGenericInstanceType([genericParameter]));

            var parameter = new InjectedParameterAnalysisContext(
                "parameter_" + i,
                genericParameter,
                ParameterAttributes.None,
                i + ParameterOffset,
                method);
            method.Parameters.Add(parameter);
        }
    }

    private static void AddReturnGenericParameterAndSetReturnType(InjectedMethodAnalysisContext method, TypeAnalysisContext iil2CppTypeGeneric)
    {
        var genericParameter = new GenericParameterTypeAnalysisContext(
            "TResult",
            method.GenericParameters.Count,
            LibCpp2IL.BinaryStructures.Il2CppTypeEnum.IL2CPP_TYPE_MVAR,
            GenericParameterAttributes.None,
            method);
        method.GenericParameters.Add(genericParameter);

        genericParameter.ConstraintTypes.Add(iil2CppTypeGeneric.MakeGenericInstanceType([genericParameter]));

        method.SetDefaultReturnType(genericParameter);
    }
}
