using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
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

                    case CilCode.Ldelema:
                        //This is Il2CppArrayBase<T>.Pointer + index * sizeof(T) but the T is not known because the operand is null.
                        return false;

                    case CilCode.Ldelem:
                        //This is Il2CppArrayBase<T>.set_Item but the T is not known because the operand is null.
                        return false;

                    case CilCode.Stelem:
                        //This is Il2CppArrayBase<T>.set_Item but the T is not known because the operand is null.
                        return false;

                    case CilCode.Ldelem_Ref:
                        //This is Il2CppReferenceArray<T>.get_Item but the T is not known because the operand is null.
                        return false;

                    case CilCode.Stelem_Ref:
                        //This is Il2CppReferenceArray<T>.set_Item but the T is not known because the operand is null.
                        return false;

                    case >= CilCode.Ldelem_I1 and <= CilCode.Ldelem_R8:
                        //This is Il2CppStructArray<T>.get_Item
                        return false;

                    case >= CilCode.Stelem_I and <= CilCode.Stelem_R8:
                        //This is Il2CppStructArray<T>.set_Item
                        return false;

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
                var fieldArg = (IFieldDescriptor)bodyInstruction.Operand;
                var fieldDeclarer =
                    Pass80UnstripMethods.ResolveTypeInNewAssembliesRaw(globalContext, fieldArg.DeclaringType!.ToTypeSignature(), imports);
                if (fieldDeclarer == null)
                    return false;
                var newField = fieldDeclarer.Resolve()?.Fields.SingleOrDefault(it => it.Name == fieldArg.Name);
                if (newField != null)
                {
                    var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, imports.Module.DefaultImporter.ImportField(newField));
                    instructionMap.Add(bodyInstruction, newInstruction);
                }
                else
                {
                    if (bodyInstruction.OpCode == OpCodes.Ldfld || bodyInstruction.OpCode == OpCodes.Ldsfld)
                    {
                        var getterMethod = fieldDeclarer.Resolve()?.Properties
                            .SingleOrDefault(it => it.Name == fieldArg.Name)?.GetMethod;
                        if (getterMethod == null)
                            return false;

                        var newInstruction = targetBuilder.Add(OpCodes.Call, imports.Module.DefaultImporter.ImportMethod(getterMethod));
                        instructionMap.Add(bodyInstruction, newInstruction);
                    }
                    else if (bodyInstruction.OpCode == OpCodes.Stfld || bodyInstruction.OpCode == OpCodes.Stsfld)
                    {
                        var setterMethod = fieldDeclarer.Resolve()?.Properties
                            .SingleOrDefault(it => it.Name == fieldArg.Name)?.SetMethod;
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
                var methodArg = (IMethodDescriptor)bodyInstruction.Operand;
                var methodDeclarer =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, methodArg.DeclaringType?.ToTypeSignature(), imports);
                if (methodDeclarer == null)
                    return false; // todo: generic methods

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

                var newMethod = new MemberReference(methodDeclarer.ToTypeDefOrRef(), methodArg.Name, newMethodSignature);

                var newInstruction = targetBuilder.Add(bodyInstruction.OpCode, imports.Module.DefaultImporter.ImportMethod(newMethod));
                instructionMap.Add(bodyInstruction, newInstruction);
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineType)
            {
                var targetType = ((ITypeDefOrRef)bodyInstruction.Operand).ToTypeSignature();
                if (targetType is GenericParameterSignature genericParam)
                {
                    if (genericParam.ParameterType is GenericParameterType.Type)
                    {
                        var newTypeOwner =
                            Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, original.DeclaringType?.ToTypeSignature(), imports)?.Resolve();
                        if (newTypeOwner == null)
                            return false;
                        targetType = newTypeOwner.GenericParameters.Single(it => it.Name == targetType.Name).ToTypeSignature();
                    }
                    else
                    {
                        targetType = target.GenericParameters.Single(it => it.Name == targetType.Name).ToTypeSignature();
                    }
                }
                else
                {
                    targetType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, targetType, imports);
                    if (targetType == null)
                        return false;
                }

                if (bodyInstruction.OpCode == OpCodes.Castclass && !targetType.IsValueType)
                {
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
                else if (bodyInstruction.OpCode == OpCodes.Newarr && !targetType.IsValueType)
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
                var targetTok = (bodyInstruction.Operand as ITypeDefOrRef)?.ToTypeSignature();
                if (targetTok == null)
                    return false;
                if (targetTok is GenericParameterSignature genericParam)
                {
                    if (genericParam.ParameterType is GenericParameterType.Type)
                    {
                        var newTypeOwner =
                            Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, original.DeclaringType?.ToTypeSignature(), imports)?.Resolve();
                        if (newTypeOwner == null)
                            return false;
                        var name = original.DeclaringType!.GenericParameters[genericParam.Index].Name;
                        targetTok = newTypeOwner.GenericParameters.Single(it => it.Name == name).ToTypeSignature();
                    }
                    else
                    {
                        var name = original.GenericParameters[genericParam.Index].Name;
                        targetTok = target.GenericParameters.Single(it => it.Name == name).ToTypeSignature();
                    }
                }
                else
                {
                    targetTok = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, targetTok, imports);
                    if (targetTok == null)
                        return false;
                }

                var newInstruction = targetBuilder.Add(OpCodes.Call,
                    imports.Module.DefaultImporter.ImportMethod(imports.Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle.Value.MakeGenericInstanceMethod(targetTok)));
                instructionMap.Add(bodyInstruction, newInstruction);
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

        return true;
    }

    public static void ReplaceBodyWithException(MethodDefinition newMethod, RuntimeAssemblyReferences imports)
    {
        newMethod.CilMethodBody!.LocalVariables.Clear();
        newMethod.CilMethodBody.Instructions.Clear();
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
