using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Passes;

namespace Il2CppInterop.Generator.Utils;

public static class UnstripTranslator
{
    public static bool TranslateMethod(MethodDefinition original, MethodDefinition target,
        TypeRewriteContext typeRewriteContext, RuntimeAssemblyReferences imports)
    {
        if (original.CilMethodBody is null) return true;

        target.CilMethodBody = new(target);

        var globalContext = typeRewriteContext.AssemblyContext.GlobalContext;
        foreach (var variableDefinition in original.CilMethodBody.LocalVariables)
        {
            var variableType =
                Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, variableDefinition.VariableType,
                    imports);
            if (variableType == null) return false;
            target.CilMethodBody.LocalVariables.Add(new CilLocalVariable(variableType));
        }

        var targetBuilder = target.CilMethodBody.Instructions;
        foreach (var bodyInstruction in original.CilMethodBody.Instructions)
            if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineField)
            {
                var fieldArg = (IFieldDescriptor?)bodyInstruction.Operand;
                var fieldDeclarer =
                    Pass80UnstripMethods.ResolveTypeInNewAssembliesRaw(globalContext, fieldArg.DeclaringType.ToTypeSignature(), imports);
                if (fieldDeclarer == null) return false;
                var newField = fieldDeclarer.Resolve().Fields.SingleOrDefault(it => it.Name == fieldArg.Name);
                if (newField != null)
                {
                    targetBuilder.Add(bodyInstruction.OpCode, imports.Module.DefaultImporter.ImportField(newField));
                }
                else
                {
                    if (bodyInstruction.OpCode == OpCodes.Ldfld || bodyInstruction.OpCode == OpCodes.Ldsfld)
                    {
                        var getterMethod = fieldDeclarer.Resolve().Properties
                            .SingleOrDefault(it => it.Name == fieldArg.Name)?.GetMethod;
                        if (getterMethod == null) return false;

                        targetBuilder.Add(OpCodes.Call, imports.Module.DefaultImporter.ImportMethod(getterMethod));
                    }
                    else if (bodyInstruction.OpCode == OpCodes.Stfld || bodyInstruction.OpCode == OpCodes.Stsfld)
                    {
                        var setterMethod = fieldDeclarer.Resolve().Properties
                            .SingleOrDefault(it => it.Name == fieldArg.Name)?.SetMethod;
                        if (setterMethod == null) return false;

                        targetBuilder.Add(OpCodes.Call, imports.Module.DefaultImporter.ImportMethod(setterMethod));
                    }
                    else
                    {
                        return false;
                    }
                }
            }
            else if (bodyInstruction.OpCode.OperandType == CilOperandType.InlineMethod)
            {
                var methodArg = (IMethodDescriptor)bodyInstruction.Operand;
                var methodDeclarer =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, methodArg.DeclaringType.ToTypeSignature(), imports);
                if (methodDeclarer == null) return false; // todo: generic methods

                var newReturnType =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext, methodArg.Signature.ReturnType, imports);
                if (newReturnType == null) return false;

                var newMethodSignature = CecilAdapter.CreateMethodSignature(!methodArg.Signature.HasThis, newReturnType);
                var newMethod = new MemberReference(methodDeclarer.ToTypeDefOrRef(), methodArg.Name, newMethodSignature);
                foreach (var methodArgParameter in methodArg.Signature.ParameterTypes)
                {
                    var newParamType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(globalContext,
                        methodArgParameter, imports);
                    if (newParamType == null) return false;

                    newMethodSignature.ParameterTypes.Add(newParamType);
                }

                targetBuilder.Add(bodyInstruction.OpCode, imports.Module.DefaultImporter.ImportMethod(newMethod));
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
                        if (newTypeOwner == null) return false;
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
                    if (targetType == null) return false;
                }

                if (bodyInstruction.OpCode == OpCodes.Castclass && !targetType.IsValueType)
                {
                    targetBuilder.Add(OpCodes.Call,
                        imports.Module.DefaultImporter.ImportMethod(imports.Il2CppObjectBase_Cast.Value.MakeGenericInstanceMethod(targetType)));
                }
                else if (bodyInstruction.OpCode == OpCodes.Isinst && !targetType.IsValueType)
                {
                    targetBuilder.Add(OpCodes.Call,
                        imports.Module.DefaultImporter.ImportMethod(imports.Il2CppObjectBase_TryCast.Value.MakeGenericInstanceMethod(targetType)));
                }
                else if (bodyInstruction.OpCode == OpCodes.Newarr && !targetType.IsValueType)
                {
                    targetBuilder.Add(OpCodes.Conv_I8);

                    var il2cppTypeArray = imports.Il2CppReferenceArray.MakeGenericInstanceType(targetType).ToTypeDefOrRef();
                    targetBuilder.Add(OpCodes.Newobj, imports.Module.DefaultImporter.ImportMethod(
                        CecilAdapter.CreateInstanceMethodReference(".ctor", imports.Module.Void(), il2cppTypeArray, imports.Module.Long())));
                }
                else
                {
                    targetBuilder.Add(bodyInstruction.OpCode, targetType.ToTypeDefOrRef());
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
                    if (targetTok == null) return false;
                }

                targetBuilder.Add(OpCodes.Call,
                    imports.Module.DefaultImporter.ImportMethod(imports.Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle.Value.MakeGenericInstanceMethod(targetTok)));
            }
            else if (bodyInstruction.Operand is null)
            {
                targetBuilder.Add(bodyInstruction.OpCode);
            }
            else
            {
                return false;
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
}
