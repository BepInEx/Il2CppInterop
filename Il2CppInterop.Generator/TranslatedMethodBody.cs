using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AsmResolver.PE.DotNet.Cil;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

public class TranslatedMethodBody : MethodBodyBase
{
    // Notes:
    //
    // There are subtle flaws in this implementation, in regards to exception handling.
    // Certain instructions (eg `castclass` and `callvirt`) can throw exceptions.
    // These exceptions will have System types, not Il2Cpp types.
    //
    //

    public static bool TryTranslateOriginalMethodBody(MethodAnalysisContext methodContext)
    {
        if (!methodContext.TryGetExtraData(out OriginalMethodBody? originalMethodBody))
        {
            return false;
        }

        var appContext = methodContext.AppContext;

        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");
        var byReference_ToPointer = byReference.GetMethodByName(nameof(ByReference<>.ToPointer));
        var byReference_GetValue = byReference.GetMethodByName(nameof(ByReference<>.GetValue));

        var byReferenceStatic = appContext.ResolveTypeOrThrow(typeof(ByReference));
        var byReferenceStatic_SetValue = byReferenceStatic.GetMethodByName(nameof(ByReference.SetValue));

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper));
        var il2CppTypeHelper_SizeOf = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.SizeOf));

        Debug.Assert(methodContext.UnsafeImplementationMethod is not null);
        var implementationMethod = methodContext.UnsafeImplementationMethod!;

        TypeConversionVisitor conversionVisitor = TypeConversionVisitor.Create(methodContext.AppContext);
        TypeReplacementVisitor replacementVisitor = TypeReplacementVisitor.CreateForMethodCopying(methodContext, implementationMethod);

        var initializeInstructions = new List<Instruction>();
        var localVariableList = new List<LocalVariable>(originalMethodBody.LocalVariables.Count);
        var localVariableDictionary = new Dictionary<LocalVariable, LocalVariable>(originalMethodBody.LocalVariables.Count);
        foreach (var originalLocalVariable in originalMethodBody.LocalVariables)
        {
            var convertedType = conversionVisitor.Replace(originalLocalVariable.Type); // Conversion to Il2Cpp types
            var transferredType = replacementVisitor.Replace(convertedType); // Swap out generic parameters for the correct ones
            var localType = byReference.MakeGenericInstanceType([transferredType]); // All locals are byref
            var translatedLocalVariable = new LocalVariable
            {
                Type = localType,
            };
            localVariableList.Add(translatedLocalVariable);
            localVariableDictionary.Add(originalLocalVariable, translatedLocalVariable);

            initializeInstructions.Add(CilOpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(transferredType));
            initializeInstructions.Add(CilOpCodes.Conv_U);
            initializeInstructions.Add(CilOpCodes.Localloc);
            initializeInstructions.Add(CilOpCodes.Newobj, byReference_Constructor.MakeConcreteGeneric([transferredType], []));
            initializeInstructions.Add(CilOpCodes.Stloc, translatedLocalVariable);
        }

        var instructionDictionary = new Dictionary<Instruction, Instruction>(originalMethodBody.Instructions.Count);
        foreach (var originalInstruction in originalMethodBody.Instructions)
        {
            instructionDictionary[originalInstruction] = new Instruction();
        }

        var translatedInstructions = new List<Instruction>(originalMethodBody.Instructions.Count);
        foreach (var originalInstruction in originalMethodBody.Instructions)
        {
            var translatedInstruction = instructionDictionary[originalInstruction];

            translatedInstructions.Add(translatedInstruction);

            var originalCode = originalInstruction.Code;
            var originalOperand = originalInstruction.Operand;

            if (originalOperand is null)
            {
                switch (originalCode.Code)
                {
                    case CilCode.Arglist:
                        return false;

                    case CilCode.Ldlen:
                        // This is Il2CppArrayBase.Length
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext.ResolveTypeOrThrow(typeof(Il2CppArrayBase)).GetMethodByName($"get_{nameof(Il2CppArrayBase.Length)}");
                        }
                        break;

                    case CilCode.Ldelem_I1:
                        // This is Il2CppArrayBase.LoadElementUnsafe<sbyte>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemSByteType]);
                        }
                        break;

                    case CilCode.Ldelem_I2:
                        // This is Il2CppArrayBase.LoadElementUnsafe<short>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemInt16Type]);
                        }
                        break;

                    case CilCode.Ldelem_I4:
                        // This is Il2CppArrayBase.LoadElementUnsafe<int>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemInt32Type]);
                        }
                        break;

                    case CilCode.Ldelem_I8:
                        // This is Il2CppArrayBase.LoadElementUnsafe<long>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemInt64Type]);
                        }
                        break;

                    case CilCode.Ldelem_U1:
                        // This is Il2CppArrayBase.LoadElementUnsafe<byte>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemByteType]);
                        }
                        break;

                    case CilCode.Ldelem_U2:
                        // This is Il2CppArrayBase.LoadElementUnsafe<ushort>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemUInt16Type]);
                        }
                        break;

                    case CilCode.Ldelem_U4:
                        // This is Il2CppArrayBase.LoadElementUnsafe<uint>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemUInt32Type]);
                        }
                        break;

                    case CilCode.Ldelem_I:
                        // This is Il2CppArrayBase.LoadElementUnsafe<nint>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemIntPtrType]);
                        }
                        break;

                    case CilCode.Ldelem_R4:
                        // This is Il2CppArrayBase.LoadElementUnsafe<float>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemSingleType]);
                        }
                        break;

                    case CilCode.Ldelem_R8:
                        // This is Il2CppArrayBase.LoadElementUnsafe<double>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.LoadElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemDoubleType]);
                        }
                        break;

                    case CilCode.Stelem_I1:
                        // This is Il2CppArrayBase.StoreElementUnsafe<sbyte>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemSByteType]);
                        }
                        break;

                    case CilCode.Stelem_I2:
                        // This is Il2CppArrayBase.StoreElementUnsafe<short>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemInt16Type]);
                        }
                        break;

                    case CilCode.Stelem_I4:
                        // This is Il2CppArrayBase.StoreElementUnsafe<int>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemInt32Type]);
                        }
                        break;

                    case CilCode.Stelem_I8:
                        // This is Il2CppArrayBase.StoreElementUnsafe<long>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemInt64Type]);
                        }
                        break;

                    case CilCode.Stelem_I:
                        // This is Il2CppArrayBase.StoreElementUnsafe<nint>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemIntPtrType]);
                        }
                        break;

                    case CilCode.Stelem_R4:
                        // This is Il2CppArrayBase.StoreElementUnsafe<float>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemSingleType]);
                        }
                        break;

                    case CilCode.Stelem_R8:
                        // This is Il2CppArrayBase.StoreElementUnsafe<double>
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext
                                .ResolveTypeOrThrow(typeof(Il2CppArrayBase))
                                .GetMethodByName(nameof(Il2CppArrayBase.StoreElementUnsafe))
                                .MakeGenericInstanceMethod([methodContext.AppContext.SystemTypes.SystemDoubleType]);
                        }
                        break;

                    case CilCode.Ldelem_Ref:
                        // This is Il2CppReferenceArray<T>.get_Item but the T is not known because the operand is null.
                        return false;

                    case CilCode.Stelem_Ref:
                        // This is Il2CppReferenceArray<T>.set_Item but the T is not known because the operand is null.
                        return false;

                    case >= CilCode.Ldind_I1 and < CilCode.Ldind_Ref:
                        // This is for by ref and pointers
                        goto default;

                    case CilCode.Ldind_Ref:
                        // This is for by ref and pointers
                        return false;

                    case CilCode.Stind_Ref:
                        // This is for by ref and pointers
                        return false;

                    case > CilCode.Stind_Ref and <= CilCode.Stind_R8:
                        // This is for by ref and pointers
                        goto default;

                    case CilCode.Refanytype:
                        // Not implemented yet
                        return false;

                    case CilCode.Throw:
                        {
                            translatedInstruction.Code = OpCodes.Callvirt;
                            translatedInstruction.Operand = methodContext.AppContext.ResolveTypeOrThrow(typeof(IIl2CppException)).GetMethodByName(nameof(IIl2CppException.CreateSystemException));
                            translatedInstructions.Add(new Instruction(originalCode));
                        }
                        break;

                    case CilCode.Ret:
                        if (MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, methodContext.ReturnType))
                        {
                            translatedInstruction.Code = OpCodes.Nop;

                            translatedInstructions.Add(new Instruction(originalCode));
                        }
                        else
                        {
                            translatedInstruction.Code = originalCode;
                        }
                        break;

                    case CilCode.Volatile:
                        // This op code can be ignored.
                        translatedInstruction.Code = OpCodes.Nop;
                        break;

                    default:
                        // nop, ldnull, ldarg_0, mul, add, etc.
                        translatedInstruction.Code = originalCode;
                        break;
                }
            }
            else if (originalOperand is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or bool or char)
            {
                translatedInstruction.Code = originalCode;
                translatedInstruction.Operand = originalOperand;
            }
            else if (originalOperand is string)
            {
                Debug.Assert(originalCode == OpCodes.Ldstr);
                translatedInstruction.Code = originalCode;
                translatedInstruction.Operand = originalOperand;

                MonoIl2CppConversion.AddMonoToIl2CppStringConversion(translatedInstructions, methodContext.AppContext);
            }
            else if (originalOperand is IReadOnlyList<ILabel> labels)
            {
                Debug.Assert(originalCode == OpCodes.Switch);
                translatedInstruction.Code = originalCode;
                translatedInstruction.Operand = ResolveLabels(labels, instructionDictionary);
            }
            else if (originalOperand is ILabel label)
            {
                translatedInstruction.Code = originalCode;
                translatedInstruction.Operand = ResolveLabel(label, instructionDictionary);
            }
            else if (originalOperand is This)
            {
                Debug.Assert(originalCode == OpCodes.Ldarg);

                var newParameter = implementationMethod.Parameters[0];

                var parameterType = (GenericInstanceTypeAnalysisContext)newParameter.ParameterType;
                var dataType = parameterType.GenericArguments[0];

                if (dataType.IsValueType)
                {
                    translatedInstruction.Code = OpCodes.Ldarg;
                    translatedInstruction.Operand = newParameter;

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, parameterType);
                }
                else
                {
                    translatedInstruction.Code = OpCodes.Ldarga;
                    translatedInstruction.Operand = newParameter;

                    translatedInstructions.Add(CilOpCodes.Call, byReference_GetValue.MakeConcreteGeneric(parameterType.GenericArguments, []));

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, dataType);
                }
            }
            else if (originalOperand is ParameterAnalysisContext parameter)
            {
                var parameterOffset = implementationMethod.Parameters.Count - methodContext.Parameters.Count;
                var newParameter = implementationMethod.Parameters[parameterOffset + parameter.ParameterIndex];

                var parameterType = (GenericInstanceTypeAnalysisContext)newParameter.ParameterType;
                var dataType = parameterType.GenericArguments[0];

                if (originalCode == OpCodes.Ldarg)
                {
                    translatedInstruction.Code = OpCodes.Ldarga;
                    translatedInstruction.Operand = newParameter;

                    translatedInstructions.Add(CilOpCodes.Call, byReference_GetValue.MakeConcreteGeneric(parameterType.GenericArguments, []));

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, dataType);
                }
                else if (originalCode == OpCodes.Starg)
                {
                    if (MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, dataType))
                    {
                        translatedInstruction.Code = OpCodes.Nop;

                        translatedInstructions.Add(OpCodes.Ldarg, newParameter);
                    }
                    else
                    {
                        translatedInstruction.Code = OpCodes.Ldarg;
                        translatedInstruction.Operand = newParameter;
                    }
                    translatedInstructions.Add(CilOpCodes.Call, byReferenceStatic_SetValue.MakeGenericInstanceMethod(parameterType.GenericArguments));
                }
                else if (originalCode == OpCodes.Ldarga)
                {
                    translatedInstruction.Code = OpCodes.Ldarg;
                    translatedInstruction.Operand = newParameter;

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, parameterType);
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return false;
                }
            }
            else if (originalOperand is LocalVariable localVariable)
            {
                var translatedLocalVariable = localVariableDictionary[localVariable];

                var localType = (GenericInstanceTypeAnalysisContext)translatedLocalVariable.Type;
                var dataType = localType.GenericArguments[0];

                if (originalCode == OpCodes.Ldloc)
                {
                    translatedInstruction.Code = OpCodes.Ldloca;
                    translatedInstruction.Operand = translatedLocalVariable;

                    translatedInstructions.Add(CilOpCodes.Call, byReference_GetValue.MakeConcreteGeneric(localType.GenericArguments, []));

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, dataType);
                }
                else if (originalCode == OpCodes.Stloc)
                {
                    if (MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, dataType))
                    {
                        translatedInstruction.Code = OpCodes.Nop;

                        translatedInstructions.Add(OpCodes.Ldloc, translatedLocalVariable);
                    }
                    else
                    {
                        translatedInstruction.Code = OpCodes.Ldloc;
                        translatedInstruction.Operand = translatedLocalVariable;
                    }
                    translatedInstructions.Add(CilOpCodes.Call, byReferenceStatic_SetValue.MakeGenericInstanceMethod(localType.GenericArguments));
                }
                else if (originalCode == OpCodes.Ldloca)
                {
                    translatedInstruction.Code = OpCodes.Ldloc;
                    translatedInstruction.Operand = translatedLocalVariable;

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, localType);
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return false;
                }
            }
            else if (originalOperand is TypeAnalysisContext type)
            {
                var convertedType = conversionVisitor.Replace(type);
                var translatedType = replacementVisitor.Replace(convertedType);

                if (originalCode.Code is CilCode.Castclass or CilCode.Constrained or CilCode.Cpobj or CilCode.Initobj or CilCode.Isinst or CilCode.Ldobj or CilCode.Stobj or CilCode.Unbox_Any)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = translatedType;
                }
                else if (originalCode == OpCodes.Sizeof)
                {
                    // Not implemented yet
                    // Probably need to use Il2CppTypeHelper.SizeOf to get the correct semantics.
                    return false;
                }
                else if (originalCode == OpCodes.Box)
                {
                    if (MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, translatedType))
                    {
                        translatedInstruction.Code = OpCodes.Nop;

                        translatedInstructions.Add(new Instruction(originalCode, translatedType));
                    }
                    else
                    {
                        translatedInstruction.Code = originalCode;
                        translatedInstruction.Operand = translatedType;
                    }
                }
                else if (originalCode == OpCodes.Unbox)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = translatedType;
                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, translatedType);
                }
                else if (originalCode == OpCodes.Ldtoken)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = translatedType;

                    translatedInstructions.Add(new Instruction(OpCodes.Call, methodContext.AppContext.SystemTypes.SystemTypeType.GetMethodByName(nameof(Type.GetTypeFromHandle))));

                    var systemTypeToIl2CppTypeMethod = methodContext.AppContext
                        .ResolveTypeOrThrow(typeof(Il2CppType))
                        .Methods.First(m => m.Name == nameof(Il2CppType.From) && m.Parameters.Count == 1);

                    translatedInstructions.Add(new Instruction(OpCodes.Call, systemTypeToIl2CppTypeMethod));

                    var getTypeHandleMethod = methodContext.AppContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Type").GetMethodByName("get_TypeHandle");

                    translatedInstructions.Add(new Instruction(OpCodes.Callvirt, getTypeHandleMethod));
                }
                else if (originalCode == OpCodes.Newarr)
                {
                    // Not implemented yet
                    return false;
                }
                else if (originalCode == OpCodes.Ldelem)
                {
                    var genericMethod = methodContext.AppContext
                        .ResolveTypeOrThrow(typeof(Il2CppArrayBase<>))
                        .GetMethodByName("get_Item");
                    translatedInstruction.Code = OpCodes.Callvirt;
                    translatedInstruction.Operand = new ConcreteGenericMethodAnalysisContext(genericMethod, [translatedType], []);
                    MonoIl2CppConversion.
                                        AddIl2CppToMonoConversion(translatedInstructions, translatedType);
                }
                else if (originalCode == OpCodes.Stelem)
                {
                    translatedInstruction.Code = OpCodes.Nop;

                    var genericMethod = methodContext.AppContext
                        .ResolveTypeOrThrow(typeof(Il2CppArrayBase<>))
                        .GetMethodByName("set_Item");
                    var concreteGenericMethod = new ConcreteGenericMethodAnalysisContext(genericMethod, [translatedType], []);
                    MonoIl2CppConversion.
                                        AddMonoToIl2CppConversion(translatedInstructions, translatedType);

                    translatedInstructions.Add(new Instruction(OpCodes.Callvirt, concreteGenericMethod));
                }
                else if (originalCode == OpCodes.Ldelema)
                {
                    // Not implemented yet
                    return false;
                }
                else if (originalCode.Code is CilCode.Mkrefany or CilCode.Refanyval)
                {
                    // Not implemented yet
                    return false;
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return false;
                }
            }
            else if (originalOperand is MethodAnalysisContext method)
            {
                MethodAnalysisContext baseMethod;
                TypeAnalysisContext[] typeGenericArguments;
                TypeAnalysisContext[] methodGenericArguments;
                if (method is ConcreteGenericMethodAnalysisContext concreteGeneric)
                {
                    baseMethod = concreteGeneric.BaseMethodContext;
                    if (concreteGeneric.TypeGenericParameters.Count > 0)
                    {
                        typeGenericArguments = new TypeAnalysisContext[concreteGeneric.TypeGenericParameters.Count];
                        for (var i = 0; i < concreteGeneric.TypeGenericParameters.Count; i++)
                        {
                            typeGenericArguments[i] = replacementVisitor.Replace(concreteGeneric.TypeGenericParameters[i]);
                        }
                    }
                    else
                    {
                        typeGenericArguments = [];
                    }
                    if (concreteGeneric.MethodGenericParameters.Count > 0)
                    {
                        methodGenericArguments = new TypeAnalysisContext[concreteGeneric.MethodGenericParameters.Count];
                        for (var i = 0; i < concreteGeneric.MethodGenericParameters.Count; i++)
                        {
                            methodGenericArguments[i] = replacementVisitor.Replace(concreteGeneric.MethodGenericParameters[i]);
                        }
                    }
                    else
                    {
                        methodGenericArguments = [];
                    }
                }
                else
                {
                    baseMethod = method;
                    typeGenericArguments = [];
                    methodGenericArguments = [];
                }
                if (originalCode.Code is CilCode.Call && baseMethod.UnsafeInvokeMethod is not null)
                {
                    translatedInstruction.Code = OpCodes.Nop;

                    var targetMethod = baseMethod.UnsafeInvokeMethod?.MaybeMakeConcreteGeneric(typeGenericArguments, methodGenericArguments);

                    Debug.Assert(targetMethod is not null);
                    Debug.Assert(targetMethod.IsStatic);
                    Debug.Assert(method.Parameters.Count == targetMethod.Parameters.Count - 1);

                    var temporaryVariables = new LocalVariable[targetMethod.Parameters.Count];
                    for (var i = targetMethod.Parameters.Count - 1; i >= 0; i--) // Order matters
                    {
                        var methodParameter = targetMethod.Parameters[i];
                        MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, methodParameter.ParameterType);
                        var temporaryVariable = new LocalVariable
                        {
                            Type = methodParameter.ParameterType,
                        };
                        translatedInstructions.Add(OpCodes.Stloc, temporaryVariable);

                        temporaryVariables[i] = temporaryVariable;
                        localVariableList.Add(temporaryVariable);
                    }

                    foreach (var temporaryVariable in temporaryVariables)
                    {
                        translatedInstructions.Add(OpCodes.Ldloc, temporaryVariable);
                    }

                    // Todo: If this is an instance method on Il2CppSystem Object/ValueType/Enum,
                    // we need to redirect it to the corresponding IObject/IValueType/IEnum method.
                    translatedInstructions.Add(originalCode, targetMethod);
                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, targetMethod.ReturnType);
                }
                else if (originalCode.Code is CilCode.Call or CilCode.Callvirt or CilCode.Newobj)
                {
                    translatedInstruction.Code = OpCodes.Nop;

                    var targetMethod = baseMethod.MaybeMakeConcreteGeneric(typeGenericArguments, methodGenericArguments);

                    var temporaryVariables = new LocalVariable[targetMethod.Parameters.Count];
                    for (var i = targetMethod.Parameters.Count - 1; i >= 0; i--) // Order matters
                    {
                        var methodParameter = targetMethod.Parameters[i];
                        MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, methodParameter.ParameterType);
                        var temporaryVariable = new LocalVariable
                        {
                            Type = methodParameter.ParameterType,
                        };
                        translatedInstructions.Add(OpCodes.Stloc, temporaryVariable);

                        temporaryVariables[i] = temporaryVariable;
                        localVariableList.Add(temporaryVariable);
                    }

                    foreach (var temporaryVariable in temporaryVariables)
                    {
                        translatedInstructions.Add(OpCodes.Ldloc, temporaryVariable);
                    }

                    // Todo: If this is an instance method on Il2CppSystem Object/ValueType/Enum,
                    // we need to redirect it to the corresponding IObject/IValueType/IEnum method.
                    translatedInstructions.Add(originalCode, targetMethod);
                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, targetMethod.ReturnType);
                }
                else if (originalCode == OpCodes.Ldtoken)
                {
                    // Not sure this can happen in normal CIL code, but we check for it just in case.
                    return false;
                }
                else if (originalCode == OpCodes.Ldftn || originalCode == OpCodes.Ldvirtftn)
                {
                    return false;
                }
                else if (originalCode == OpCodes.Jmp)
                {
                    // This shouldn't happen in normal CIL code, but we check for it just in case.
                    return false;
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return false;
                }
            }
            else if (originalOperand is FieldAnalysisContext field)
            {
                FieldAnalysisContext baseField;
                TypeAnalysisContext[] declaringTypeArguments;
                if (field is ConcreteGenericFieldAnalysisContext concreteGenericField)
                {
                    baseField = concreteGenericField.BaseFieldContext;
                    declaringTypeArguments = replacementVisitor.Replace(((GenericInstanceTypeAnalysisContext)field.DeclaringType).GenericArguments).ToArray();
                }
                else
                {
                    baseField = field;
                    declaringTypeArguments = [];
                }

                if (originalCode == OpCodes.Ldfld || originalCode == OpCodes.Ldsfld)
                {
                    // Load field value
                    if (baseField.PropertyAccessor is not null)
                    {
                        Debug.Assert(baseField.IsStatic || baseField is { DeclaringType.IsValueType: false }, "Value types should not have instance field accessors.");
                        var accessorMethod = baseField.PropertyAccessor!.Getter!.MaybeMakeConcreteGeneric(declaringTypeArguments, []);
                        translatedInstruction.Code = originalCode == OpCodes.Ldfld ? OpCodes.Callvirt : OpCodes.Call;
                        translatedInstruction.Operand = accessorMethod;
                    }
                    else if (baseField.DeclaringType.Fields.Contains(baseField))
                    {
                        // This is wrong! It should be getting the field offset, adding it to the object pointer, and then dereferencing it.
                        Debug.Assert(!baseField.IsStatic, "There should be no static fields.");
                        Debug.Assert(baseField is { DeclaringType.IsValueType: true }, "Only value types should have instance fields.");
                        translatedInstruction.Code = originalCode;
                        translatedInstruction.Operand = baseField.MaybeMakeConcreteGeneric(declaringTypeArguments);
                    }
                    else
                    {
                        // This should not occur.
                        return false;
                    }

                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, field.FieldType);
                }
                else if (originalCode == OpCodes.Stfld || originalCode == OpCodes.Stsfld)
                {
                    // Store field value

                    translatedInstruction.Code = OpCodes.Nop;
                    MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, field.FieldType);

                    if (baseField.PropertyAccessor is not null)
                    {
                        Debug.Assert(baseField.IsStatic || baseField is { DeclaringType.IsValueType: false }, "Value types should not have instance field accessors.");
                        var accessorMethod = baseField.PropertyAccessor!.Setter!.MaybeMakeConcreteGeneric(declaringTypeArguments, []);
                        translatedInstructions.Add(new Instruction(originalCode == OpCodes.Stfld ? OpCodes.Callvirt : OpCodes.Call, accessorMethod));
                    }
                    else if (baseField.DeclaringType.Fields.Contains(baseField))
                    {
                        // This is wrong! It should be getting the field offset, adding it to the object pointer, and then dereferencing it.
                        Debug.Assert(!baseField.IsStatic, "There should be no static fields.");
                        Debug.Assert(baseField is { DeclaringType.IsValueType: true }, "Only value types should have instance fields.");
                        translatedInstructions.Add(new Instruction(originalCode, originalOperand));
                    }
                    else
                    {
                        // This should not occur.
                        return false;
                    }
                }
                else if (originalCode == OpCodes.Ldflda || originalCode == OpCodes.Ldsflda)
                {
                    // Load field address
                    // Not implemented yet
                    return false;
                }
                else if (originalCode == OpCodes.Ldtoken)
                {
                    // Not sure this can happen in normal CIL code, but we check for it just in case.
                    return false;
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return false;
                }
            }
            else if (originalOperand is MultiDimensionalArrayMethod)
            {
                // Not implemented yet
                return false;
            }
            else
            {
                return false;
            }
        }

        var exceptionHandlers = new ExceptionHandler[originalMethodBody.ExceptionHandlers.Count];
        for (var i = 0; i < exceptionHandlers.Length; i++)
        {
            var originalExceptionHandler = originalMethodBody.ExceptionHandlers[i];

            var handlerStart = ResolveLabel(originalExceptionHandler.HandlerStart, instructionDictionary);

            TypeAnalysisContext? exceptionType;
            if (originalExceptionHandler.ExceptionType is null)
            {
                exceptionType = null;
            }
            else if (IsIl2CppPrimitive(originalExceptionHandler.ExceptionType, "Object"))
            {
                exceptionType = methodContext.AppContext.SystemTypes.SystemObjectType;
            }
            else
            {
                if (originalExceptionHandler.ExceptionType is GenericInstanceTypeAnalysisContext genericInstance)
                {
                    exceptionType = genericInstance.GenericType.SystemExceptionType!.MakeGenericInstanceType(replacementVisitor.Replace(genericInstance.GenericArguments));
                }
                else
                {
                    exceptionType = originalExceptionHandler.ExceptionType.SystemExceptionType;
                    Debug.Assert(exceptionType is not null);
                }

                // The system exception wrapper contains a reference to the underlying Il2Cpp object.
                // We need to load the object reference after entering the exception handler.

                Debug.Assert(handlerStart is Instruction);
                var handlerStartIndex = translatedInstructions.IndexOf((Instruction)handlerStart);

                var loadObjectInstruction = new Instruction(OpCodes.Ldfld, methodContext.AppContext.ResolveTypeOrThrow(typeof(Il2CppException)).GetFieldByName(nameof(Il2CppException.Il2cppObject)));
                var castInstruction = new Instruction(OpCodes.Castclass, originalExceptionHandler.ExceptionType);
                translatedInstructions.Insert(handlerStartIndex, loadObjectInstruction);
                translatedInstructions.Insert(handlerStartIndex + 1, castInstruction);

                handlerStart = loadObjectInstruction;
            }

            var translatedExceptionHandler = new ExceptionHandler
            {
                HandlerType = originalExceptionHandler.HandlerType,
                TryStart = ResolveLabel(originalExceptionHandler.TryStart, instructionDictionary),
                TryEnd = ResolveLabel(originalExceptionHandler.TryEnd, instructionDictionary),
                HandlerStart = handlerStart,
                HandlerEnd = ResolveLabel(originalExceptionHandler.HandlerEnd, instructionDictionary),
                FilterStart = ResolveLabel(originalExceptionHandler.FilterStart, instructionDictionary),
                ExceptionType = exceptionType,
            };
            exceptionHandlers[i] = translatedExceptionHandler;
        }

        translatedInstructions.InsertRange(0, initializeInstructions);

        implementationMethod.PutExtraData(new TranslatedMethodBody
        {
            Instructions = translatedInstructions,
            LocalVariables = localVariableList,
            ExceptionHandlers = exceptionHandlers,
        });
        return true;

        static ILabel[] ResolveLabels(IReadOnlyList<ILabel> labels, Dictionary<Instruction, Instruction> instructionDictionary)
        {
            var resolvedLabels = new ILabel[labels.Count];
            for (var i = 0; i < labels.Count; i++)
            {
                resolvedLabels[i] = ResolveLabel(labels[i], instructionDictionary);
            }
            return resolvedLabels;
        }

        [return: NotNullIfNotNull(nameof(label))]
        static ILabel? ResolveLabel(ILabel? label, Dictionary<Instruction, Instruction> instructionDictionary)
        {
            if (label is null or EndLabel)
                return label;

            return instructionDictionary[(Instruction)label];
        }
    }

    private static bool IsIl2CppPrimitive(TypeAnalysisContext type, string name)
    {
        if (type is ReferencedTypeAnalysisContext)
            return false;

        if (type.DeclaringType is not null)
            return false;

        if (type.DeclaringAssembly != type.AppContext.Il2CppMscorlib)
            return false;

        return type.Namespace == "Il2CppSystem" && type.Name == name;
    }
}
