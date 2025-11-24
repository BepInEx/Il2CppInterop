using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AssetRipper.CIL;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils.AsmResolver;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.StackTypes;
using Il2CppInterop.Generator.Visitors;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;

namespace Il2CppInterop.Generator;

public abstract class MethodBodyBase
{
    public required IReadOnlyList<Instruction> Instructions { get; init; }
    public IReadOnlyList<LocalVariable> LocalVariables { get; init; } = [];
    public IReadOnlyList<ExceptionHandler> ExceptionHandlers { get; init; } = [];

    public void FillMethodBody(MethodDefinition method)
    {
        if (!method.IsManagedMethodWithBody())
        {
            return;
        }

        var body = new CilMethodBody()
        {
            InitializeLocals = LocalVariables.Count > 0
        };
        method.CilMethodBody = body;
        var module = method.DeclaringModule!;
        var instructions = body.Instructions;

        var labels = new Dictionary<ILabel, ICilLabel>(Instructions.Count + 1);
        for (var i = Instructions.Count - 1; i >= 0; i--)
        {
            labels.Add(Instructions[i], new CilInstructionLabel());
        }
        labels.Add(EndLabel.Instance, instructions.EndLabel);

        foreach (var exceptionHandler in ExceptionHandlers)
        {
            var handler = new CilExceptionHandler
            {
                HandlerType = exceptionHandler.HandlerType,
                TryStart = labels.GetValue(exceptionHandler.TryStart),
                TryEnd = labels.GetValue(exceptionHandler.TryEnd),
                HandlerStart = labels.GetValue(exceptionHandler.HandlerStart),
                HandlerEnd = labels.GetValue(exceptionHandler.HandlerEnd) ?? instructions.EndLabel,
                FilterStart = labels.GetValue(exceptionHandler.FilterStart),
                ExceptionType = exceptionHandler.ExceptionType?.ToTypeSignature(module).ToTypeDefOrRef(),
            };
            body.ExceptionHandlers.Add(handler);
        }

        Dictionary<LocalVariable, CilLocalVariable> localVariableMap = new(LocalVariables.Count);
        for (var i = LocalVariables.Count - 1; i >= 0; i--)
        {
            var localVariable = LocalVariables[i];
            var localVariableType = localVariable.Type.ToTypeSignature(module);
            var value = new CilLocalVariable(localVariableType);
            body.LocalVariables.Add(value);
            localVariableMap.Add(localVariable, value);
        }

        for (var i = 0; i < Instructions.Count; i++)
        {
            var instruction = Instructions[i];
            var operand = instruction.Operand switch
            {
                This => method.Parameters.ThisParameter ?? throw new NullReferenceException("This parameter should not be null."),
                LocalVariable localVariable => localVariableMap[localVariable],
                TypeAnalysisContext type => type.ToTypeSignature(module).ToTypeDefOrRef(),
                MethodAnalysisContext methodOperand => methodOperand.ToMethodDescriptor(module),
                FieldAnalysisContext field => field.ToFieldDescriptor(module),
                ParameterAnalysisContext parameter => method.Parameters[parameter.ParameterIndex],
                MultiDimensionalArrayMethod arrayMethod => arrayMethod.ToMethodDescriptor(module),
                ILabel label => labels[label],
                IReadOnlyList<ILabel> labelArray => labelArray.Select(labels.GetValue).ToArray(),
                _ => instruction.Operand,
            };
            var cilInstruction = new CilInstruction(instruction.Code, operand);
            instructions.Add(cilInstruction);
            ((CilInstructionLabel)labels[instruction]).Instruction = cilInstruction;
        }

        instructions.OptimizeMacros();
    }

    internal void AnalyzeControlFlow(out Dictionary<Instruction, List<Instruction>> instructionPredecessors, out Dictionary<Instruction, List<Instruction>> instructionSuccessors)
    {
        instructionPredecessors = new Dictionary<Instruction, List<Instruction>>(Instructions.Count);
        instructionSuccessors = new Dictionary<Instruction, List<Instruction>>(Instructions.Count);
        foreach (var instruction in Instructions)
        {
            instructionPredecessors[instruction] = [];
            instructionSuccessors[instruction] = [];
        }
        for (var i = 0; i < Instructions.Count; i++)
        {
            var instruction = Instructions[i];
            if (instruction.Code.FlowControl is CilFlowControl.Return or CilFlowControl.Throw)
            {
                // No successors.
            }
            else if (instruction.Operand is ILabel label)
            {
                Debug.Assert(instruction.Code.FlowControl is CilFlowControl.Branch or CilFlowControl.ConditionalBranch);
                Instruction? targetInstruction = label as Instruction;
                if (targetInstruction is not null)
                {
                    instructionSuccessors[instruction].Add(targetInstruction);
                    instructionPredecessors[targetInstruction].Add(instruction);
                }

                if (instruction.Code.Code is CilCode.Br or CilCode.Leave)
                {
                    // Unconditional branches do not have fallthrough.
                }
                else if (i + 1 >= Instructions.Count)
                {
                    // No fallthrough, this is the last instruction.
                }
                else if (Instructions[i + 1] == targetInstruction)
                {
                    // Do nothing, already added above.
                }
                else
                {
                    var nextInstruction = Instructions[i + 1];
                    instructionSuccessors[instruction].Add(nextInstruction);
                    instructionPredecessors[nextInstruction].Add(instruction);
                }
            }
            else if (instruction.Operand is IReadOnlyList<ILabel> labels)
            {
                Debug.Assert(instruction.Code.FlowControl is CilFlowControl.ConditionalBranch);
                HashSet<Instruction> targetInstructions = new();
                foreach (var label1 in labels)
                {
                    if (label1 is Instruction targetInstruction && targetInstructions.Add(targetInstruction))
                    {
                        instructionSuccessors[instruction].Add(targetInstruction);
                        instructionPredecessors[targetInstruction].Add(instruction);
                    }
                }
                if (i + 1 < Instructions.Count)
                {
                    var nextInstruction = Instructions[i + 1];
                    if (!targetInstructions.Contains(nextInstruction))
                    {
                        instructionSuccessors[instruction].Add(nextInstruction);
                        instructionPredecessors[nextInstruction].Add(instruction);
                    }
                }
            }
            else
            {
                Debug.Assert(instruction.Code.FlowControl is CilFlowControl.Break or CilFlowControl.Call or CilFlowControl.Meta or CilFlowControl.Next);
                if (i + 1 < Instructions.Count)
                {
                    var nextInstruction = Instructions[i + 1];
                    instructionSuccessors[instruction].Add(nextInstruction);
                    instructionPredecessors[nextInstruction].Add(instruction);
                }
            }
        }
    }

    internal Dictionary<Instruction, StackType[]> AnalyzeStackTypes(MethodAnalysisContext owner, TypeReplacementVisitor? visitor = null, bool il2CppTypes = false)
    {
        AnalyzeControlFlow(out var instructionPredecessors, out var instructionSuccessors);

        Dictionary<Instruction, StackType[]> result = new(Instructions.Count);

        if (Instructions.Count == 0)
        {
            return result;
        }

        visitor ??= TypeReplacementVisitor.Null;

        result[Instructions[0]] = [];

        foreach (var exceptionHandler in ExceptionHandlers)
        {
            if (exceptionHandler.HandlerType is CilExceptionHandlerType.Finally)
            {
                if (exceptionHandler.HandlerStart is Instruction instruction)
                {
                    Debug.Assert(instructionPredecessors[instruction].Count == 0);
                    result.Add(instruction, []);
                }
            }
            else
            {
                StackType exceptionType = exceptionHandler.ExceptionType is null ? UnknownStackType.Instance : GetExactType(exceptionHandler.ExceptionType, visitor);

                if (exceptionHandler.HandlerStart is Instruction handlerInstruction)
                {
                    Debug.Assert(instructionPredecessors[handlerInstruction].Count == 0);
                    result.Add(handlerInstruction, [exceptionType]);
                }

                if (exceptionHandler.FilterStart is Instruction filterInstruction)
                {
                    Debug.Assert(instructionPredecessors[filterInstruction].Count == 0);
                    result.Add(filterInstruction, [exceptionType]);
                }
            }
        }

        bool changed;
        do
        {
            changed = false;

            foreach (var instruction in Instructions)
            {
                if (!result.TryGetValue(instruction, out var stackInitial))
                {
                    continue;
                }

                var popCount = instruction.GetPopCount(owner);
                if (popCount is -1)
                {
                    // -1 is special case for "pop all"
                    popCount = stackInitial.Length;
                }
                var stackAfterPop = stackInitial.AsSpan(0, stackInitial.Length - popCount);

                var poppedTypes = stackInitial.AsSpan(stackAfterPop.Length, stackInitial.Length - stackAfterPop.Length);

                var pushCount = instruction.GetPushCount(owner);
                var stackAfterPushSize = stackAfterPop.Length + pushCount;
                Span<StackType> stackAfterPush = stackAfterPushSize is 0 ? [] : new StackType[stackAfterPushSize];
                stackAfterPop.CopyTo(stackAfterPush);

                // Set pushed types
                if (pushCount is 1)
                {
                    stackAfterPush[^1] = instruction.Code.Code switch
                    {
                        CilCode.Add or CilCode.Sub or CilCode.Mul or CilCode.Div or CilCode.Rem
                            or CilCode.Add_Ovf or CilCode.Add_Ovf_Un or CilCode.Sub_Ovf or CilCode.Sub_Ovf_Un
                            or CilCode.Mul_Ovf or CilCode.Mul_Ovf_Un or CilCode.Div_Un or CilCode.Rem_Un
                            or CilCode.And or CilCode.Or or CilCode.Xor => StackType.MergeForMathOperation(poppedTypes[0], poppedTypes[1]),
                        CilCode.Neg or CilCode.Not => poppedTypes[0],
                        CilCode.Shl or CilCode.Shr or CilCode.Shr_Un => poppedTypes[0],
                        CilCode.Ldobj => GetExactType((TypeAnalysisContext)instruction.Operand!, visitor),
                        CilCode.Castclass or CilCode.Isinst => GetExactType((TypeAnalysisContext)instruction.Operand!, visitor),
                        CilCode.Ldstr => GetExactType(il2CppTypes ? owner.AppContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.String") : owner.AppContext.SystemTypes.SystemStringType, visitor),
                        CilCode.Ldlen => IntegerStackType32.Instance,
                        CilCode.Ceq or CilCode.Cgt or CilCode.Cgt_Un or CilCode.Clt or CilCode.Clt_Un => IntegerStackType32.Instance,
                        CilCode.Sizeof => IntegerStackType32.Instance,
                        CilCode.Ldind_I
                            or CilCode.Ldelem_I
                            or CilCode.Conv_I or CilCode.Conv_U
                            or CilCode.Conv_Ovf_I or CilCode.Conv_Ovf_U
                            or CilCode.Conv_Ovf_I_Un or CilCode.Conv_Ovf_U_Un => IntegerStackTypeNative.Instance,
                        CilCode.Ldc_I4 or CilCode.Ldind_I1 or CilCode.Ldind_I2 or CilCode.Ldind_I4
                            or CilCode.Ldind_U1 or CilCode.Ldind_U2 or CilCode.Ldind_U4
                            or CilCode.Ldelem_I1 or CilCode.Ldelem_I2 or CilCode.Ldelem_I4
                            or CilCode.Ldelem_U1 or CilCode.Ldelem_U2 or CilCode.Ldelem_U4
                            or CilCode.Conv_I1 or CilCode.Conv_I2 or CilCode.Conv_I4
                            or CilCode.Conv_U1 or CilCode.Conv_U2 or CilCode.Conv_U4
                            or CilCode.Conv_Ovf_I1 or CilCode.Conv_Ovf_I2 or CilCode.Conv_Ovf_I4
                            or CilCode.Conv_Ovf_I1_Un or CilCode.Conv_Ovf_I2_Un or CilCode.Conv_Ovf_I4_Un
                            or CilCode.Conv_Ovf_U1 or CilCode.Conv_Ovf_U2 or CilCode.Conv_Ovf_U4
                            or CilCode.Conv_Ovf_U1_Un or CilCode.Conv_Ovf_U2_Un or CilCode.Conv_Ovf_U4_Un => IntegerStackType32.Instance,
                        CilCode.Ldc_I8 or CilCode.Ldind_I8 or CilCode.Ldelem_I8
                            or CilCode.Conv_I8 or CilCode.Conv_U8
                            or CilCode.Conv_Ovf_I8 or CilCode.Conv_Ovf_U8
                            or CilCode.Conv_Ovf_I8_Un or CilCode.Conv_Ovf_U8_Un => IntegerStackType64.Instance,
                        CilCode.Ldc_R4 or CilCode.Ldind_R4 or CilCode.Ldelem_R4 or CilCode.Conv_R4 => SingleStackType.Instance,
                        CilCode.Ldc_R8 or CilCode.Ldind_R8 or CilCode.Ldelem_R8 or CilCode.Conv_R8 or CilCode.Conv_R_Un => DoubleStackType.Instance,
                        CilCode.Ldarg_0 or CilCode.Ldarg_1 or CilCode.Ldarg_2 or CilCode.Ldarg_3 => throw new InvalidOperationException("Ldarg_* should have been replaced with Ldarg."),
                        CilCode.Ldloc_0 or CilCode.Ldloc_1 or CilCode.Ldloc_2 or CilCode.Ldloc_3 => throw new InvalidOperationException("Ldloc_* should have been replaced with Ldloc."),
                        CilCode.Ldarg or CilCode.Ldarg_S => instruction.Operand switch
                        {
                            ParameterAnalysisContext parameter => GetExactType(visitor.Replace(parameter.ParameterType), visitor),
                            This => owner.DeclaringType!.IsValueType
                                ? GetExactType(visitor.Replace(owner.DeclaringType).MakeByReferenceType(), visitor)
                                : GetExactType(owner.DeclaringType, visitor),
                            _ => throw new InvalidOperationException("Ldarg operand should be ParameterAnalysisContext or This"),
                        },
                        CilCode.Ldloc or CilCode.Ldloc_S => instruction.Operand switch
                        {
                            LocalVariable localVariable => GetExactType(localVariable.Type, visitor),
                            _ => throw new InvalidOperationException("Ldloc operand should be LocalVariable"),
                        },
                        CilCode.Ldarga or CilCode.Ldarga_S => instruction.Operand switch
                        {
                            ParameterAnalysisContext parameter => GetExactType(parameter.ParameterType.MakeByReferenceType(), visitor),
                            This => throw new NotSupportedException("Ldarga on 'this' is not supported."),
                            _ => throw new InvalidOperationException("Ldarg operand should be ParameterAnalysisContext or This"),
                        },
                        CilCode.Ldloca or CilCode.Ldloca_S => instruction.Operand switch
                        {
                            LocalVariable localVariable => GetExactType(localVariable.Type.MakeByReferenceType(), visitor),
                            _ => throw new InvalidOperationException("Ldloc operand should be LocalVariable"),
                        },
                        CilCode.Newobj => instruction.Operand switch
                        {
                            MethodAnalysisContext methodOperand => GetExactType(methodOperand.DeclaringType!, visitor),
                            MultiDimensionalArrayMethod arrayMethod => GetExactType(arrayMethod.ArrayType, visitor),
                            _ => throw new InvalidOperationException("Newobj operand should be MethodAnalysisContext or MultiDimensionalArrayMethod"),
                        },
                        CilCode.Call or CilCode.Callvirt => instruction.Operand switch
                        {
                            MethodAnalysisContext methodOperand => GetExactType(methodOperand.ReturnType, visitor),
                            MultiDimensionalArrayMethod arrayMethod => arrayMethod.MethodType switch
                            {
                                MultiDimensionalArrayMethodType.Get => GetExactType(arrayMethod.ArrayType.ElementType, visitor),
                                MultiDimensionalArrayMethodType.Address => GetExactType(arrayMethod.ArrayType.ElementType.MakeByReferenceType(), visitor),
                                _ => throw new NotSupportedException($"Method type {arrayMethod.MethodType} does not return a value."),
                            },
                            _ => throw new InvalidOperationException("Call/Callvirt operand should be MethodAnalysisContext or MultiDimensionalArrayMethod"),
                        },
                        CilCode.Ldfld or CilCode.Ldsfld => GetExactType(((FieldAnalysisContext)instruction.Operand!).FieldType, visitor),
                        CilCode.Ldflda or CilCode.Ldsflda => GetExactType(((FieldAnalysisContext)instruction.Operand!).FieldType.MakeByReferenceType(), visitor),
                        CilCode.Ldind_Ref => GetStackType_Ldind_Ref(stackInitial, visitor),
                        CilCode.Ldelem_Ref => GetStackType_Ldelem_Ref(stackInitial, visitor),
                        CilCode.Ldelema => GetExactType(((TypeAnalysisContext)instruction.Operand!).MakeByReferenceType(), visitor),
                        CilCode.Ldelem => GetExactType((TypeAnalysisContext)instruction.Operand!, visitor),
                        CilCode.Box => GetExactType((TypeAnalysisContext)instruction.Operand!, visitor),
                        CilCode.Unbox or CilCode.Unbox_Any => GetExactType((TypeAnalysisContext)instruction.Operand!, visitor),
                        CilCode.Newarr => GetExactType(((TypeAnalysisContext)instruction.Operand!).MakeSzArrayType(), visitor),
                        CilCode.Ldnull => UnknownStackType.Instance,
                        CilCode.Ldftn or CilCode.Ldvirtftn => IntegerStackTypeNative.Instance,
                        CilCode.Localloc => IntegerStackTypeNative.Instance,
                        CilCode.Ldtoken => UnknownStackType.Instance,// Todo
                        CilCode.Mkrefany => UnknownStackType.Instance,// Todo
                        CilCode.Refanytype => UnknownStackType.Instance,// Todo
                        CilCode.Refanyval => UnknownStackType.Instance,// Todo
                        CilCode.Arglist => UnknownStackType.Instance,// Todo
                        _ => UnknownStackType.Instance,
                    };
                }
                else if (pushCount is 2)
                {
                    Debug.Assert(instruction.Code == CilOpCodes.Dup);
                    stackAfterPush[^2] = stackAfterPush[^1] = poppedTypes[0];
                }
                else
                {
                    Debug.Assert(pushCount is 0);
                }

                foreach (var successor in instructionSuccessors[instruction])
                {
                    if (result.TryGetValue(successor, out var existingStack))
                    {
                        if (existingStack.Length != stackAfterPush.Length)
                        {
                            throw new InvalidOperationException($"Inconsistent stack heights at instruction {successor}: existing {existingStack.Length}, new {stackAfterPush.Length}");
                        }

                        for (var i = 0; i < existingStack.Length; i++)
                        {
                            var merged = StackType.Merge(existingStack[i], stackAfterPush[i]);
                            if (!EqualityComparer<StackType>.Default.Equals(merged, existingStack[i]))
                            {
                                existingStack[i] = merged;
                                changed = true;
                            }
                        }
                    }
                    else
                    {
                        result[successor] = stackAfterPush.ToArray();
                        changed = true;
                    }
                }
            }
        } while (changed);

        Debug.Assert(result.Count == Instructions.Count);

        return result;
    }

    private static StackType GetStackType_Ldind_Ref(StackType[] stackInitial, TypeReplacementVisitor visitor)
    {
        return stackInitial[^1] is ExactStackType exactType && TryGetByReferenceElementType(exactType.Type, out var elementType)
            ? GetExactType(elementType!, visitor)
            : UnknownStackType.Instance;
    }

    private static StackType GetStackType_Ldelem_Ref(StackType[] stackInitial, TypeReplacementVisitor visitor)
    {
        return stackInitial[^2] is ExactStackType exactType && TryGetArrayElementType(exactType.Type, out var elementType)
            ? GetExactType(elementType!, visitor)
            : UnknownStackType.Instance;
    }

    private static StackType GetExactType(TypeAnalysisContext type, TypeReplacementVisitor visitor)
    {
        var replacedType = visitor.Replace(type);
        if (IsPointerType(replacedType))
        {
            return IntegerStackTypeNative.Instance;
        }
        if (IsEnum(replacedType, out var enumElementType))
        {
            return GetExactType(enumElementType, visitor);
        }
        return replacedType.KnownType switch
        {
            KnownTypeCode.Il2CppSystem_Boolean => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_Char => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_Byte => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_SByte => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_Int16 => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_UInt16 => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_Int32 => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_UInt32 => IntegerStackType32.Instance,
            KnownTypeCode.Il2CppSystem_Int64 => IntegerStackType64.Instance,
            KnownTypeCode.Il2CppSystem_UInt64 => IntegerStackType64.Instance,
            KnownTypeCode.Il2CppSystem_IntPtr => IntegerStackTypeNative.Instance,
            KnownTypeCode.Il2CppSystem_UIntPtr => IntegerStackTypeNative.Instance,
            KnownTypeCode.Il2CppSystem_Single => SingleStackType.Instance,
            KnownTypeCode.Il2CppSystem_Double => DoubleStackType.Instance,
            _ => new ExactStackType(replacedType),
        };
    }

    private static bool IsPointerType(TypeAnalysisContext type)
    {
        if (type is PointerTypeAnalysisContext)
            return true;

        return type is GenericInstanceTypeAnalysisContext { GenericType: { Name: $"{nameof(Pointer<>)}`1" } genericType } && genericType.Namespace == typeof(Pointer<>).Namespace;
    }

    protected static bool IsByReferenceType(TypeAnalysisContext type)
    {
        return TryGetByReferenceElementType(type, out _);
    }

    protected static bool TryGetByReferenceElementType(TypeAnalysisContext type, [NotNullWhen(true)] out TypeAnalysisContext? elementType)
    {
        if (type is ByRefTypeAnalysisContext byRefType)
        {
            elementType = byRefType.ElementType;
            return true;
        }
        if (type is GenericInstanceTypeAnalysisContext { GenericType: { Name: $"{nameof(ByReference<>)}`1" } genericType } && genericType.Namespace == typeof(ByReference<>).Namespace)
        {
            elementType = ((GenericInstanceTypeAnalysisContext)type).GenericArguments[0];
            return true;
        }
        elementType = null;
        return false;
    }

    private static bool TryGetArrayElementType(TypeAnalysisContext type, [NotNullWhen(true)] out TypeAnalysisContext? elementType)
    {
        if (type is ArrayTypeAnalysisContext arrayType)
        {
            elementType = arrayType.ElementType;
            return true;
        }
        if (type is SzArrayTypeAnalysisContext szArrayType)
        {
            elementType = szArrayType.ElementType;
            return true;
        }
        if (type is GenericInstanceTypeAnalysisContext { GenericType: { Name: $"{nameof(Il2CppArrayRank1<>)}`1" } genericType } && genericType.Namespace == typeof(Il2CppArrayRank1<>).Namespace)
        {
            elementType = ((GenericInstanceTypeAnalysisContext)type).GenericArguments[0];
            return true;
        }
        elementType = null;
        return false;
    }

    private static bool IsEnum(TypeAnalysisContext type, [NotNullWhen(true)] out TypeAnalysisContext? elementType)
    {
        if (type is { DefaultBaseType.KnownType: KnownTypeCode.Il2CppSystem_Enum or KnownTypeCode.System_Enum })
        {
            elementType = type.Fields.FirstOrDefault(f => f.Name == "value__")?.FieldType;
            return elementType is not null;
        }
        elementType = null;
        return false;
    }
}
