using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using AsmResolver.PE.DotNet.Cil;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

public class TranslatedMethodBody : MethodBodyBase
{
    public static bool MaybeStoreTranslatedMethodBody(MethodAnalysisContext method)
    {
        if (TryTranslateOriginalMethodBody(method, out var translatedMethodBody))
        {
            method.PutExtraData(translatedMethodBody);
            return true;
        }
        else
        {
            return false;
        }
    }

    // Notes:
    //
    // There are subtle flaws in this implementation, in regards to exception handling.
    // Certain instructions (eg `castclass` and `callvirt`) can throw exceptions.
    // These exceptions will have System types, not Il2Cpp types.
    //
    // 

    private static bool TryTranslateOriginalMethodBody(MethodAnalysisContext methodContext, [NotNullWhen(true)] out TranslatedMethodBody? translatedMethodBody)
    {
        if (!methodContext.TryGetExtraData(out OriginalMethodBody? originalMethodBody))
        {
            translatedMethodBody = null;
            return false;
        }

        TypeConversionVisitor visitor = TypeConversionVisitor.Create(methodContext.AppContext);

        var localVariableList = new List<LocalVariable>(originalMethodBody.LocalVariables.Count);
        var localVariableDictionary = new Dictionary<LocalVariable, LocalVariable>(originalMethodBody.LocalVariables.Count);
        foreach (var originalLocalVariable in originalMethodBody.LocalVariables)
        {
            var translatedLocalVariable = new LocalVariable
            {
                Type = visitor.Replace(originalLocalVariable.Type),
            };
            localVariableList.Add(translatedLocalVariable);
            localVariableDictionary.Add(originalLocalVariable, translatedLocalVariable);
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
                        return False(out translatedMethodBody);

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
                        return False(out translatedMethodBody);

                    case CilCode.Stelem_Ref:
                        // This is Il2CppReferenceArray<T>.set_Item but the T is not known because the operand is null.
                        return False(out translatedMethodBody);

                    case >= CilCode.Ldind_I1 and < CilCode.Ldind_Ref:
                        // This is for by ref and pointers
                        goto default;

                    case CilCode.Ldind_Ref:
                        // This is for by ref and pointers
                        return False(out translatedMethodBody);

                    case CilCode.Stind_Ref:
                        // This is for by ref and pointers
                        return False(out translatedMethodBody);

                    case > CilCode.Stind_Ref and <= CilCode.Stind_R8:
                        // This is for by ref and pointers
                        goto default;

                    case CilCode.Refanytype:
                        // Not implemented yet
                        return False(out translatedMethodBody);

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
                translatedInstruction.Code = originalCode;
                translatedInstruction.Operand = originalOperand;
            }
            else if (originalOperand is ParameterAnalysisContext parameter)
            {
                if (originalCode == OpCodes.Ldarg)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = originalOperand;
                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, parameter.ParameterType);
                }
                else if (originalCode == OpCodes.Starg)
                {
                    if (MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, parameter.ParameterType))
                    {
                        translatedInstruction.Code = OpCodes.Nop;

                        translatedInstructions.Add(new Instruction(originalCode, originalOperand));
                    }
                    else
                    {
                        translatedInstruction.Code = originalCode;
                        translatedInstruction.Operand = originalOperand;
                    }
                }
                else if (originalCode == OpCodes.Ldarga)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = originalOperand;
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return False(out translatedMethodBody);
                }
            }
            else if (originalOperand is LocalVariable localVariable)
            {
                var translatedLocalVariable = localVariableDictionary[localVariable];

                if (originalCode == OpCodes.Ldloc)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = translatedLocalVariable;
                    MonoIl2CppConversion.AddIl2CppToMonoConversion(translatedInstructions, translatedLocalVariable.Type);
                }
                else if (originalCode == OpCodes.Stloc)
                {
                    if (MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, translatedLocalVariable.Type))
                    {
                        translatedInstruction.Code = OpCodes.Nop;

                        translatedInstructions.Add(new Instruction(originalCode, translatedLocalVariable));
                    }
                    else
                    {
                        translatedInstruction.Code = originalCode;
                        translatedInstruction.Operand = translatedLocalVariable;
                    }
                }
                else if (originalCode == OpCodes.Ldloca)
                {
                    // Everything is fine. No conversion needed.
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = translatedLocalVariable;
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return False(out translatedMethodBody);
                }
            }
            else if (originalOperand is TypeAnalysisContext type)
            {
                var translatedType = visitor.Replace(type);

                if (originalCode.Code is CilCode.Castclass or CilCode.Constrained or CilCode.Cpobj or CilCode.Initobj or CilCode.Isinst or CilCode.Ldobj or CilCode.Stobj or CilCode.Unbox_Any)
                {
                    translatedInstruction.Code = originalCode;
                    translatedInstruction.Operand = translatedType;
                }
                else if (originalCode == OpCodes.Sizeof)
                {
                    // Not implemented yet
                    // Probably need to use Il2CppTypeHelper.SizeOf to get the correct semantics.
                    return False(out translatedMethodBody);
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
                    return False(out translatedMethodBody);
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
                    return False(out translatedMethodBody);
                }
                else if (originalCode.Code is CilCode.Mkrefany or CilCode.Refanyval)
                {
                    // Not implemented yet
                    return False(out translatedMethodBody);
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return False(out translatedMethodBody);
                }
            }
            else if (originalOperand is MethodAnalysisContext method)
            {
                if (originalCode.Code is CilCode.Call or CilCode.Callvirt or CilCode.Newobj)
                {
                    translatedInstruction.Code = OpCodes.Nop;

                    var temporaryVariables = new LocalVariable[method.Parameters.Count];
                    for (var i = method.Parameters.Count - 1; i >= 0; i--) // Order matters
                    {
                        var methodParameter = method.Parameters[i];
                        MonoIl2CppConversion.AddMonoToIl2CppConversion(translatedInstructions, methodParameter.ParameterType);
                        var temporaryVariable = new LocalVariable
                        {
                            Type = methodParameter.ParameterType,
                        };
                        translatedInstructions.Add(new Instruction(OpCodes.Stloc, temporaryVariable));

                        temporaryVariables[i] = temporaryVariable;
                        localVariableList.Add(temporaryVariable);
                    }

                    foreach (var temporaryVariable in temporaryVariables)
                    {
                        translatedInstructions.Add(new Instruction(OpCodes.Ldloc, temporaryVariable));
                    }

                    // Todo: If this is an instance method on Il2CppSystem Object/ValueType/Enum,
                    // we need to redirect it to the corresponding IObject/IValueType/IEnum method.
                    translatedInstructions.Add(new Instruction(originalCode, method));
                    MonoIl2CppConversion.
                                        AddIl2CppToMonoConversion(translatedInstructions, method.ReturnType);
                }
                else if (originalCode == OpCodes.Ldtoken)
                {
                    // Not sure this can happen in normal CIL code, but we check for it just in case.
                    return False(out translatedMethodBody);
                }
                else if (originalCode == OpCodes.Ldftn || originalCode == OpCodes.Ldvirtftn)
                {
                    return False(out translatedMethodBody);
                }
                else if (originalCode == OpCodes.Jmp)
                {
                    // This shouldn't happen in normal CIL code, but we check for it just in case.
                    return False(out translatedMethodBody);
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return False(out translatedMethodBody);
                }
            }
            else if (originalOperand is FieldAnalysisContext field)
            {
                var baseField = (field as ConcreteGenericFieldAnalysisContext)?.BaseFieldContext ?? field;

                if (originalCode == OpCodes.Ldfld || originalCode == OpCodes.Ldsfld)
                {
                    // Load field value
                    if (baseField.PropertyAccessor is not null)
                    {
                        var accessorMethod = field is ConcreteGenericFieldAnalysisContext
                            ? new ConcreteGenericMethodAnalysisContext(baseField.PropertyAccessor!.Getter!, ((GenericInstanceTypeAnalysisContext)field.DeclaringType).GenericArguments, [])
                            : baseField.PropertyAccessor!.Getter!;
                        translatedInstruction.Code = originalCode == OpCodes.Ldfld ? OpCodes.Callvirt : OpCodes.Call;
                        translatedInstruction.Operand = accessorMethod;
                    }
                    else if (baseField.DeclaringType.Fields.Contains(baseField))
                    {
                        translatedInstruction.Code = originalCode;
                        translatedInstruction.Operand = originalOperand;
                    }
                    else
                    {
                        // This should not occur.
                        return False(out translatedMethodBody);
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
                        var accessorMethod = field is ConcreteGenericFieldAnalysisContext
                            ? new ConcreteGenericMethodAnalysisContext(baseField.PropertyAccessor!.Setter!, ((GenericInstanceTypeAnalysisContext)field.DeclaringType).GenericArguments, [])
                            : baseField.PropertyAccessor!.Setter!;

                        translatedInstructions.Add(new Instruction(originalCode == OpCodes.Stfld ? OpCodes.Callvirt : OpCodes.Call, accessorMethod));
                    }
                    else if (baseField.DeclaringType.Fields.Contains(baseField))
                    {
                        translatedInstructions.Add(new Instruction(originalCode, originalOperand));
                    }
                    else
                    {
                        // This should only occur for special cases like String::_firstChar.
                        return False(out translatedMethodBody);
                    }
                }
                else if (originalCode == OpCodes.Ldflda || originalCode == OpCodes.Ldsflda)
                {
                    // Load field address
                    // Not implemented yet
                    return False(out translatedMethodBody);
                }
                else if (originalCode == OpCodes.Ldtoken)
                {
                    // Not sure this can happen in normal CIL code, but we check for it just in case.
                    return False(out translatedMethodBody);
                }
                else
                {
                    Debug.Fail($"Unexpected CIL code: {originalCode}");
                    return False(out translatedMethodBody);
                }
            }
            else if (originalOperand is MultiDimensionalArrayMethod)
            {
                // Not implemented yet
                return False(out translatedMethodBody);
            }
            else
            {
                return False(out translatedMethodBody);
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
                    exceptionType = genericInstance.GenericType.SystemExceptionType!.MakeGenericInstanceType(genericInstance.GenericArguments);
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

        translatedMethodBody = new TranslatedMethodBody
        {
            Instructions = translatedInstructions,
            LocalVariables = localVariableList,
            ExceptionHandlers = exceptionHandlers,
        };
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

        static bool False([NotNullWhen(true)] out TranslatedMethodBody? translatedMethodBody)
        {
            translatedMethodBody = null;
            return false;
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
