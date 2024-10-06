using System.Diagnostics;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Passes;

namespace Il2CppInterop.Generator.Utils;

public static class UnstripTranslator
{
    public static bool TranslateMethod(MethodDefinition original, MethodDefinition target,
        TypeRewriteContext typeRewriteContext, RuntimeAssemblyReferences imports)
    {
        if (original.CilMethodBody is null)
            return true;

        target.CilMethodBody = new(target);

        var globalContext = typeRewriteContext.AssemblyContext.GlobalContext;
        Dictionary<CilLocalVariable, CilLocalVariable> localVariableMap = new();
        foreach (var variableDefinition in original.CilMethodBody.LocalVariables)
        {
            var variableType =
                Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, variableDefinition.VariableType,
                    imports);
            if (variableType == null)
                return false;
            var newVariableDefinition = new CilLocalVariable(variableType);
            target.CilMethodBody.LocalVariables.Add(newVariableDefinition);
            localVariableMap.Add(variableDefinition, newVariableDefinition);
        }

        // We expand macros because our instructions are not mapped one-to-one,
        // so specialized instructions like Br_S need to be expanded to Br for safety.
        // In pass 90, we optimize all macros, so we won't need to worry about that here.
        original.CilMethodBody.Instructions.ExpandMacros();

        List<KeyValuePair<CilInstructionLabel, CilInstructionLabel>> labelMap = new();
        Dictionary<CilInstruction, CilInstruction> instructionMap = new();

        var targetBuilder = target.CilMethodBody.Instructions;
        foreach (var bodyInstruction in original.CilMethodBody.Instructions)
        {
            if (bodyInstruction.Operand is null)
            {
                CilInstruction newInstruction;
                switch (bodyInstruction.OpCode.Code)
                {
                    case CilCode.Ldlen:
                        //This is Il2CppArrayBase.Length
                        newInstruction = targetBuilder.Add(OpCodes.Callvirt,
                            imports.Module.DefaultImporter.ImportMethod(imports.Il2CppArrayBase_get_Length.Value));
                        break;

                    case CilCode.Ldelem_Ref:
                        //This is Il2CppReferenceArray<T>.get_Item but the T is not known because the operand is null.
                        return false;

                    case CilCode.Stelem_Ref:
                        //This is Il2CppReferenceArray<T>.set_Item but the T is not known because the operand is null.
                        return false;

                    case CilCode.Ldelem_I1:
                    case CilCode.Ldelem_I2:
                    case CilCode.Ldelem_I4:
                    case CilCode.Ldelem_U4:
                        //This is Il2CppArrayBase<T>.get_Item but the T could be either the cooresponding primitive or an enum.
                        return false;

                    case CilCode.Ldelem_U1:
                        //This is Il2CppArrayBase<T>.get_Item but the T could be either byte, bool, or an enum.
                        return false;

                    case CilCode.Ldelem_U2:
                        //This is Il2CppArrayBase<T>.get_Item but the T could be either ushort, char, or an enum.
                        return false;

                    case CilCode.Ldelem_I8:
                        //This is Il2CppArrayBase<T>.get_Item but the T could be either signed, unsigned, or an enum.
                        return false;

                    case CilCode.Ldelem_I:
                        //This is Il2CppArrayBase<T>.get_Item but the T could be either signed, unsigned, or a pointer.
                        return false;

                    case CilCode.Ldelem_R4:
                        {
                            var getMethod = imports.Il2CppArrayBase_get_Item.Get(imports.Module.CorLibTypeFactory.Single);
                            newInstruction = targetBuilder.Add(OpCodes.Callvirt, imports.Module.DefaultImporter.ImportMethod(getMethod));
                        }
                        break;

                    case CilCode.Ldelem_R8:
                        {
                            var getMethod = imports.Il2CppArrayBase_get_Item.Get(imports.Module.CorLibTypeFactory.Double);
                            newInstruction = targetBuilder.Add(OpCodes.Callvirt, imports.Module.DefaultImporter.ImportMethod(getMethod));
                        }
                        break;

                    case >= CilCode.Stelem_I and <= CilCode.Stelem_I8:
                        //This is Il2CppStructArray<T>.set_Item
                        return false;

                    case CilCode.Stelem_R4:
                        {
                            var setMethod = imports.Il2CppArrayBase_set_Item.Get(imports.Module.CorLibTypeFactory.Single);
                            newInstruction = targetBuilder.Add(OpCodes.Callvirt, imports.Module.DefaultImporter.ImportMethod(setMethod));
                        }
                        break;

                    case CilCode.Stelem_R8:
                        {
                            var setMethod = imports.Il2CppArrayBase_set_Item.Get(imports.Module.CorLibTypeFactory.Double);
                            newInstruction = targetBuilder.Add(OpCodes.Callvirt, imports.Module.DefaultImporter.ImportMethod(setMethod));
                        }
                        break;

                    case >= CilCode.Ldind_I1 and <= CilCode.Ldind_Ref:
                        //This is for by ref parameters
                        goto default;

                    case >= CilCode.Stind_Ref and <= CilCode.Stind_R8:
                        //This is for by ref parameters
                        goto default;

                    default:
                        //Noop, ldnull, ldarg_0, mul, add, etc.
                        newInstruction = targetBuilder.Add(bodyInstruction.OpCode);
                        break;
                }

                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineField)
            {
                // This code doesn't handle fields in the corlib types well.
                // Static fields are fine, but references to instance fields can't be redirected.

                var fieldArg = (IFieldDescriptor)bodyInstruction.Operand;
                var useSystemCorlibType = fieldArg.Signature?.HasThis ?? true;
                var fieldDeclarer =
                    Pass80UnstripMethods.ResolveTypeInNewAssembliesRaw(globalContext, fieldArg.DeclaringType!.ToTypeSignature(), imports, useSystemCorlibType);
                if (fieldDeclarer == null)
                    return false;
                var fieldDeclarerDefinition = fieldDeclarer.Resolve();
                if (fieldDeclarerDefinition == null)
                    return false;

                var fieldDeclarerContext = globalContext.GetContextForNewType(fieldDeclarerDefinition);
                var propertyName = fieldDeclarerContext.Fields.SingleOrDefault(it => it.OriginalField.Name == fieldArg.Name)?.UnmangledName;

                var newField = fieldDeclarerDefinition.Fields.SingleOrDefault(it => it.Name == fieldArg.Name)
                    ?? fieldDeclarerDefinition.Fields.SingleOrDefault(it => it.Name == propertyName);
                if (newField != null)
                {
                    var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, imports.Module.DefaultImporter.ImportField(newField));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else
                {
                    if (propertyName == null)
                    {
                        return false;
                    }
                    else if (bodyInstruction.OpCode == OpCodes.Ldfld || bodyInstruction.OpCode == OpCodes.Ldsfld)
                    {
                        var getterMethod = fieldDeclarerDefinition.Properties
                            .SingleOrDefault(it => it.Name == propertyName)?.GetMethod;
                        if (getterMethod == null)
                            return false;

                        var newInstruction = targetBuilder.Add(OpCodes.Call, imports.Module.DefaultImporter.ImportMethod(getterMethod));
                        instructionMap.Add(bodyInstruction, newInstruction);
                    }
                    else if (bodyInstruction.OpCode == OpCodes.Stfld || bodyInstruction.OpCode == OpCodes.Stsfld)
                    {
                        var setterMethod = fieldDeclarerDefinition.Properties
                            .SingleOrDefault(it => it.Name == propertyName)?.SetMethod;
                        if (setterMethod == null)
                            return false;

                        var newInstruction = targetBuilder.Add(OpCodes.Call, imports.Module.DefaultImporter.ImportMethod(setterMethod));
                        instructionMap.Add(bodyInstruction, newInstruction);
                    }
                    else
                    {
                        //Ldflda, Ldsflda
                        return false;
                    }
                }
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineMethod)
            {
                // This code doesn't handle methods in the corlib types well.
                // Static methods are fine, but references to instance methods can't be redirected.

                var methodArg = (IMethodDescriptor)bodyInstruction.Operand;
                var useSystemCorlibType = methodArg.Signature?.HasThis ?? true;
                var methodDeclarer =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, methodArg.DeclaringType?.ToTypeSignature(), imports, useSystemCorlibType);
                if (methodDeclarer == null)
                    return false;

                var newReturnType =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, methodArg.Signature?.ReturnType, imports);
                if (newReturnType == null)
                    return false;

                var newMethodSignature = methodArg.Signature!.HasThis
                    ? MethodSignature.CreateInstance(newReturnType, methodArg.Signature.GenericParameterCount)
                    : MethodSignature.CreateStatic(newReturnType, methodArg.Signature.GenericParameterCount);
                foreach (var methodArgParameter in methodArg.Signature.ParameterTypes)
                {
                    var newParamType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext,
                        methodArgParameter, imports);
                    if (newParamType == null)
                        return false;

                    newMethodSignature.ParameterTypes.Add(newParamType);
                }

                var memberReference = new MemberReference(methodDeclarer.ToTypeDefOrRef(), methodArg.Name, newMethodSignature);

                IMethodDescriptor newMethod;
                if (methodArg is MethodSpecification genericMethod)
                {
                    if (genericMethod.Signature is null)
                        return false;

                    TypeSignature[] typeArguments = new TypeSignature[genericMethod.Signature.TypeArguments.Count];
                    for (var i = 0; i < genericMethod.Signature.TypeArguments.Count; i++)
                    {
                        var newTypeArgument = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, genericMethod.Signature.TypeArguments[i], imports);
                        if (newTypeArgument == null)
                            return false;

                        typeArguments[i] = newTypeArgument;
                    }

                    newMethod = memberReference.MakeGenericInstanceMethod(typeArguments);
                }
                else
                {
                    newMethod = memberReference;
                }

                var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, imports.Module.DefaultImporter.ImportMethod(newMethod));
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineType)
            {
                var targetType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, ((ITypeDefOrRef)bodyInstruction.Operand).ToTypeSignature(), imports);
                if (targetType == null)
                    return false;

                if ((bodyInstruction.OpCode == OpCodes.Castclass && !targetType.IsValueType) ||
                    (bodyInstruction.OpCode == OpCodes.Unbox_Any && targetType is GenericParameterSignature))
                {
                    // Compilers use unbox.any for casting to generic parameter types.
                    // Castclass is only used for reference types.
                    // Both can be translated to Il2CppObjectBase.Cast<T>().
                    var newInstruction = targetBuilder.Add(OpCodes.Call,
                        imports.Module.DefaultImporter.ImportMethod(imports.Il2CppObjectBase_Cast.Value.MakeGenericInstanceMethod(targetType)));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else if (bodyInstruction.OpCode == OpCodes.Isinst && !targetType.IsValueType)
                {
                    var newInstruction = targetBuilder.Add(OpCodes.Call,
                        imports.Module.DefaultImporter.ImportMethod(imports.Il2CppObjectBase_TryCast.Value.MakeGenericInstanceMethod(targetType)));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else if (bodyInstruction.OpCode == OpCodes.Newarr)
                {
                    var newInstruction = targetBuilder.Add(OpCodes.Conv_I8);

                    ITypeDefOrRef il2cppTypeArray;
                    if (targetType.IsValueType)
                    {
                        return false;
                    }
                    else if (targetType.FullName == "System.String")
                    {
                        il2cppTypeArray = imports.Il2CppStringArray.ToTypeDefOrRef();
                    }
                    else
                    {
                        il2cppTypeArray = imports.Il2CppReferenceArray.MakeGenericInstanceType(targetType).ToTypeDefOrRef();
                    }
                    targetBuilder.Add(OpCodes.Newobj, imports.Module.DefaultImporter.ImportMethod(
                        ReferenceCreator.CreateInstanceMethodReference(".ctor", imports.Module.Void(), il2cppTypeArray, imports.Module.Long())));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else if (bodyInstruction.OpCode == OpCodes.Ldelema)
                {
                    // Not implemented
                    return false;
                }
                else if (bodyInstruction.OpCode == OpCodes.Ldelem)
                {
                    var getMethod = imports.Il2CppArrayBase_get_Item.Get(targetType);
                    var newInstruction = targetBuilder.Add(OpCodes.Callvirt, imports.Module.DefaultImporter.ImportMethod(getMethod));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else if (bodyInstruction.OpCode == OpCodes.Stelem)
                {
                    var setMethod = imports.Il2CppArrayBase_set_Item.Get(targetType);
                    var newInstruction = targetBuilder.Add(OpCodes.Callvirt, imports.Module.DefaultImporter.ImportMethod(setMethod));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else
                {
                    var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, targetType.ToTypeDefOrRef());
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineSig)
            {
                // todo: rewrite sig if this ever happens in unity types
                return false;
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineTok)
            {
                Debug.Assert(bodyInstruction.OpCode.Code is CilCode.Ldtoken);
                switch (bodyInstruction.Operand)
                {
                    case ITypeDefOrRef typeDefOrRef:
                        {
                            var targetTok = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, typeDefOrRef.ToTypeSignature(), imports);
                            if (targetTok == null)
                                return false;

                            var newInstruction = targetBuilder.Add(OpCodes.Call,
                                imports.Module.DefaultImporter.ImportMethod(imports.Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle.Value.MakeGenericInstanceMethod(targetTok)));
                            instructionMap.Add(bodyInstruction, newInstruction);
                        }
                        break;
                    default:
                        // Ldtoken is also used for members, which is not implemented.
                        return false;
                }
            }
            else if (bodyInstruction.OpCode.OperandType is CilOperandType.InlineSwitch && bodyInstruction.Operand is IReadOnlyList<ICilLabel> labels)
            {
                List<ICilLabel> newLabels = new(labels.Count);
                for (var i = 0; i < labels.Count; i++)
                {
                    if (labels[i] is CilInstructionLabel oldLabel)
                    {
                        var newLabel = new CilInstructionLabel();
                        labelMap.Add(new(oldLabel, newLabel));
                        newLabels.Add(newLabel);
                    }
                    else
                    {
                        return false;
                    }
                }
                var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, newLabels);
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.Operand is string or Utf8String
                || bodyInstruction.Operand.GetType().IsPrimitive)
            {
                var newInstruction = new CilInstruction(bodyInstruction.OpCode, bodyInstruction.Operand);
                targetBuilder.Add(newInstruction);
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.Operand is Parameter parameter)
            {
                var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, target.Parameters.GetBySignatureIndex(parameter.MethodSignatureIndex));
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.Operand is CilLocalVariable localVariable)
            {
                var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, localVariableMap[localVariable]);
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.Operand is CilInstructionLabel label)
            {
                var newLabel = new CilInstructionLabel();
                labelMap.Add(new(label, newLabel));
                var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, newLabel);
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else
            {
                return false;
            }
        }

        foreach ((var oldLabel, var newLabel) in labelMap)
        {
            newLabel.Instruction = instructionMap[oldLabel.Instruction!];
        }

        // Copy exception handlers
        foreach (var exceptionHandler in original.CilMethodBody.ExceptionHandlers)
        {
            var newExceptionHandler = new CilExceptionHandler
            {
                HandlerType = exceptionHandler.HandlerType
            };

            switch (exceptionHandler.TryStart)
            {
                case null:
                    break;
                case CilInstructionLabel { Instruction: not null } tryStart:
                    newExceptionHandler.TryStart = new CilInstructionLabel(instructionMap[tryStart.Instruction]);
                    break;
                default:
                    return false;
            }

            switch (exceptionHandler.TryEnd)
            {
                case null:
                    break;
                case CilInstructionLabel { Instruction: not null } tryEnd:
                    newExceptionHandler.TryEnd = new CilInstructionLabel(instructionMap[tryEnd.Instruction]);
                    break;
                default:
                    return false;
            }

            switch (exceptionHandler.HandlerStart)
            {
                case null:
                    break;
                case CilInstructionLabel { Instruction: not null } handlerStart:
                    newExceptionHandler.HandlerStart = new CilInstructionLabel(instructionMap[handlerStart.Instruction]);
                    break;
                default:
                    return false;
            }

            switch (exceptionHandler.HandlerEnd)
            {
                case null:
                    break;
                case CilInstructionLabel { Instruction: not null } handlerEnd:
                    newExceptionHandler.HandlerEnd = new CilInstructionLabel(instructionMap[handlerEnd.Instruction]);
                    break;
                default:
                    return false;
            }

            switch (exceptionHandler.FilterStart)
            {
                case null:
                    break;
                case CilInstructionLabel { Instruction: not null } filterStart:
                    newExceptionHandler.FilterStart = new CilInstructionLabel(instructionMap[filterStart.Instruction]);
                    break;
                default:
                    return false;
            }

            switch (exceptionHandler.ExceptionType?.ToTypeSignature())
            {
                case null:
                    break;
                case CorLibTypeSignature { ElementType: ElementType.Object }:
                    newExceptionHandler.ExceptionType = imports.Module.CorLibTypeFactory.Object.ToTypeDefOrRef();
                    break;
                default:
                    // In the future, we will throw exact exceptions, but we don't right now,
                    // so attempting to catch a specific exception type will always fail.
                    return false;
            }

            target.CilMethodBody.ExceptionHandlers.Add(newExceptionHandler);
        }

        return true;
    }

    public static void ReplaceBodyWithException(MethodDefinition newMethod, RuntimeAssemblyReferences imports)
    {
        newMethod.CilMethodBody = new(newMethod);
        var processor = newMethod.CilMethodBody.Instructions;

        processor.Add(OpCodes.Ldstr, "Method unstripping failed");
        processor.Add(OpCodes.Newobj, imports.Module.NotSupportedExceptionCtor());
        processor.Add(OpCodes.Throw);
        processor.Add(OpCodes.Ret);
    }

    //Required for deconstruction on net472
    private static void Deconstruct(this KeyValuePair<CilInstructionLabel, CilInstructionLabel> pair, out CilInstructionLabel key, out CilInstructionLabel value)
    {
        key = pair.Key;
        value = pair.Value;
    }
}
