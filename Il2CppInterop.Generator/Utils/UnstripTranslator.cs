using System.Linq;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Passes;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Utils;

public class UnstripTranslator
{
    public static bool TranslateMethod(MethodDefinition original, MethodDefinition target,
        TypeRewriteContext typeRewriteContext, RuntimeAssemblyReferences imports)
    {
        var translator = new UnstripTranslator(original, target, typeRewriteContext, imports);
        return translator.Translate();
    }

    private readonly MethodDefinition _original, _target;
    private readonly RuntimeAssemblyReferences _imports;

    private readonly RewriteGlobalContext _globalContext;
    private readonly ILProcessor _targetBuilder;

    private UnstripTranslator(MethodDefinition original, MethodDefinition target,
        TypeRewriteContext typeRewriteContext, RuntimeAssemblyReferences imports)
    {
        _original = original;
        _target = target;
        _imports = imports;

        _globalContext = typeRewriteContext.AssemblyContext.GlobalContext;
        _targetBuilder = target.Body.GetILProcessor();
    }

    private bool Translate()
    {
        if (!_original.HasBody) return true;

        foreach (var variableDefinition in _original.Body.Variables)
        {
            var variableType =
                Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, variableDefinition.VariableType,
                    _imports);
            if (variableType == null) return false;
            _target.Body.Variables.Add(new VariableDefinition(variableType));
        }

        foreach (var bodyInstruction in _original.Body.Instructions)
            if (!Translate(bodyInstruction))
                return false;

        return true;
    }

    private bool Translate(Instruction ins)
    {
        return ins.OpCode.OperandType switch
        {
            OperandType.InlineField => InlineField(ins),
            OperandType.InlineMethod => InlineMethod(ins),
            OperandType.InlineType => InlineType(ins),
            OperandType.InlineSig => InlineSig(ins),
            OperandType.InlineTok => InlineTok(ins),
            _ => Copy(ins),
        };
    }

    private bool InlineField(Instruction ins)
    {
        var fieldArg = (FieldReference)ins.Operand;
        var fieldDeclarer =
            Pass80UnstripMethods.ResolveTypeInNewAssembliesRaw(_globalContext, fieldArg.DeclaringType, _imports);
        if (fieldDeclarer == null) return false;

        var newField = fieldDeclarer.Resolve().Fields.SingleOrDefault(it => it.Name == fieldArg.Name);
        if (newField != null)
        {
            _targetBuilder.Emit(ins.OpCode, _imports.Module.ImportReference(newField));
            return true;
        }

        if (ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Ldsfld)
        {
            var getterMethod = fieldDeclarer.Resolve().Properties
                .SingleOrDefault(it => it.Name == fieldArg.Name)?.GetMethod;
            if (getterMethod == null) return false;

            _targetBuilder.Emit(OpCodes.Call, _imports.Module.ImportReference(getterMethod));
            return true;
        }

        if (ins.OpCode == OpCodes.Stfld || ins.OpCode == OpCodes.Stsfld)
        {
            var setterMethod = fieldDeclarer.Resolve().Properties
                .SingleOrDefault(it => it.Name == fieldArg.Name)?.SetMethod;
            if (setterMethod == null) return false;

            _targetBuilder.Emit(OpCodes.Call, _imports.Module.ImportReference(setterMethod));
            return true;
        }

        return false;
    }

    private bool InlineMethod(Instruction ins)
    {
        var methodArg = (MethodReference)ins.Operand;
        var methodDeclarer =
            Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, methodArg.DeclaringType, _imports);
        if (methodDeclarer == null) return false; // todo: generic methods

        var newReturnType =
            Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, methodArg.ReturnType, _imports);
        if (newReturnType == null) return false;

        var newMethod = new MethodReference(methodArg.Name, newReturnType, methodDeclarer);
        newMethod.HasThis = methodArg.HasThis;
        foreach (var methodArgParameter in methodArg.Parameters)
        {
            var newParamType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext,
                methodArgParameter.ParameterType, _imports);
            if (newParamType == null) return false;

            var newParam = new ParameterDefinition(methodArgParameter.Name, methodArgParameter.Attributes,
                newParamType);
            newMethod.Parameters.Add(newParam);
        }

        _targetBuilder.Emit(ins.OpCode, _imports.Module.ImportReference(newMethod));
        return true;
    }

    private bool InlineType(Instruction ins)
    {
        var targetType = (TypeReference)ins.Operand;
        if (targetType is GenericParameter genericParam)
        {
            if (genericParam.Owner is TypeReference paramOwner)
            {
                var newTypeOwner =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, paramOwner, _imports);
                if (newTypeOwner == null) return false;
                targetType = newTypeOwner.GenericParameters.Single(it => it.Name == targetType.Name);
            }
            else
            {
                targetType = _target.GenericParameters.Single(it => it.Name == targetType.Name);
            }
        }
        else
        {
            targetType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, targetType, _imports);
            if (targetType == null) return false;
        }

        if (ins.OpCode == OpCodes.Castclass && !targetType.IsValueType)
        {
            _targetBuilder.Emit(OpCodes.Call,
                _imports.Module.ImportReference(new GenericInstanceMethod(_imports.Il2CppObjectBase_Cast.Value)
                { GenericArguments = { targetType } }));
            return true;
        }

        if (ins.OpCode == OpCodes.Isinst && !targetType.IsValueType)
        {
            _targetBuilder.Emit(OpCodes.Call,
                _imports.Module.ImportReference(new GenericInstanceMethod(_imports.Il2CppObjectBase_TryCast.Value)
                { GenericArguments = { targetType } }));
            return true;
        }

        if (ins.OpCode == OpCodes.Newarr && !targetType.IsValueType)
        {
            _targetBuilder.Emit(OpCodes.Conv_I8);

            var il2cppTypeArray = new GenericInstanceType(_imports.Il2CppReferenceArray)
            { GenericArguments = { targetType } };
            _targetBuilder.Emit(OpCodes.Newobj, _imports.Module.ImportReference(
                new MethodReference(".ctor", _imports.Module.Void(), il2cppTypeArray)
                {
                    HasThis = true,
                    Parameters = { new ParameterDefinition(_imports.Module.Long()) }
                }));
            return true;
        }

        _targetBuilder.Emit(ins.OpCode, targetType);
        return true;
    }

    private bool InlineSig(Instruction ins)
    {
        // todo: rewrite sig if this ever happens in unity types
        return false;
    }

    private bool InlineTok(Instruction ins)
    {
        var targetTok = (TypeReference)ins.Operand;
        if (targetTok is GenericParameter genericParam)
        {
            if (genericParam.Owner is TypeReference paramOwner)
            {
                var newTypeOwner =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, paramOwner, _imports);
                if (newTypeOwner == null) return false;
                targetTok = newTypeOwner.GenericParameters.Single(it => it.Name == targetTok.Name);
            }
            else
            {
                targetTok = _target.GenericParameters.Single(it => it.Name == targetTok.Name);
            }
        }
        else
        {
            targetTok = Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, targetTok, _imports);
            if (targetTok == null) return false;
        }

        _targetBuilder.Emit(OpCodes.Call,
            _imports.Module.ImportReference(
                new GenericInstanceMethod(_imports.Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle.Value)
                { GenericArguments = { targetTok } }));
        return true;
    }

    private bool Copy(Instruction ins)
    {
        _targetBuilder.Append(ins);
        return true;
    }

    public static void ReplaceBodyWithException(MethodDefinition newMethod, RuntimeAssemblyReferences imports)
    {
        newMethod.Body.Variables.Clear();
        newMethod.Body.Instructions.Clear();
        var processor = newMethod.Body.GetILProcessor();

        processor.Emit(OpCodes.Ldstr, "Method unstripping failed");
        processor.Emit(OpCodes.Newobj, imports.Module.NotSupportedExceptionCtor());
        processor.Emit(OpCodes.Throw);
        processor.Emit(OpCodes.Ret);
    }
}

