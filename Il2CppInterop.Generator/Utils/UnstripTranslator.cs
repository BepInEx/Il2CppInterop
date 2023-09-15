using System.Linq;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Passes;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Utils;

public class UnstripTranslator
{

    public readonly struct Result
    {
        public static readonly Result OK = new(ErrorType.None, null, null);
        public static Result Unimplemented(Instruction ins) =>
            new(ErrorType.Unimplemented, ins, null);

        public readonly ErrorType type;
        public readonly Instruction offendingInstruction;
        public readonly string reason;
        public bool IsError => type != ErrorType.None;

        public Result(ErrorType type, Instruction offendingInstruction,  string reason)
        {
            this.type = type;
            this.offendingInstruction = offendingInstruction;
            this.reason = reason;
        }
    }

    public enum ErrorType
    {
        None,
        Unimplemented,
        Unresolved,
        FieldProxy,
    }

    public static Result TranslateMethod(MethodDefinition original, MethodDefinition target,
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

    private Result Translate()
    {
        if (!_original.HasBody) return Result.OK;

        foreach (var variableDefinition in _original.Body.Variables)
        {
            var variableType =
                Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, variableDefinition.VariableType,
                    _imports);
            if (variableType == null)
                return new(ErrorType.Unresolved, null, $"Could not resolve variable #{variableDefinition.Index} {variableDefinition.VariableType}");
            _target.Body.Variables.Add(new VariableDefinition(variableType));
        }

        foreach (var bodyInstruction in _original.Body.Instructions)
        {
            var result = Translate(bodyInstruction);
            if (result.IsError)
                return result;
        }

        return Result.OK;
    }

    private Result Translate(Instruction ins)
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

    private Result InlineField(Instruction ins)
    {
        var fieldArg = (FieldReference)ins.Operand;
        var fieldDeclarer =
            Pass80UnstripMethods.ResolveTypeInNewAssembliesRaw(_globalContext, fieldArg.DeclaringType, _imports);
        if (fieldDeclarer == null)
            return new(ErrorType.Unresolved, ins, $"Could not resolve declaring type {fieldArg.DeclaringType}");

        var newField = fieldDeclarer.Resolve().Fields.SingleOrDefault(it => it.Name == fieldArg.Name);
        if (newField != null)
        {
            _targetBuilder.Emit(ins.OpCode, _imports.Module.ImportReference(newField));
            return Result.OK;
        }

        if (ins.OpCode == OpCodes.Ldfld || ins.OpCode == OpCodes.Ldsfld)
        {
            var getterMethod = fieldDeclarer.Resolve().Properties
                .SingleOrDefault(it => it.Name == fieldArg.Name)?.GetMethod;
            if (getterMethod == null)
                return new(ErrorType.FieldProxy, ins, $"Could not find getter for proxy property {fieldArg}");

            _targetBuilder.Emit(OpCodes.Call, _imports.Module.ImportReference(getterMethod));
            return Result.OK;
        }

        if (ins.OpCode == OpCodes.Stfld || ins.OpCode == OpCodes.Stsfld)
        {
            var setterMethod = fieldDeclarer.Resolve().Properties
                .SingleOrDefault(it => it.Name == fieldArg.Name)?.SetMethod;
            if (setterMethod == null)
                return new(ErrorType.FieldProxy, ins, $"Could not find setter for proxy property {fieldArg}");

            _targetBuilder.Emit(OpCodes.Call, _imports.Module.ImportReference(setterMethod));
            return Result.OK;
        }

        return Result.Unimplemented(ins);
    }

    private Result InlineMethod(Instruction ins)
    {
        var methodArg = (MethodReference)ins.Operand;
        var methodDeclarer =
            Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, methodArg.DeclaringType, _imports);
        if (methodDeclarer == null)
            return new(ErrorType.Unresolved, ins, $"Could not resolve declaring type {methodArg.DeclaringType}");

        var newReturnType = methodArg.ReturnType switch
        {
            GenericParameter genericParam => genericParam,
            _ => Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, methodArg.ReturnType, _imports),
        };
        if (newReturnType == null)
            return new(ErrorType.Unresolved, ins, $"Could not resolve return type {methodArg.ReturnType}");

        var newMethod = new MethodReference(methodArg.Name, newReturnType, methodDeclarer);
        newMethod.HasThis = methodArg.HasThis;
        foreach (var methodArgParameter in methodArg.Parameters)
        {
            var newParamType = methodArgParameter.ParameterType switch
            {
                GenericParameter genericParam => genericParam,
                _ => Pass80UnstripMethods.ResolveTypeInNewAssemblies(
                    _globalContext, methodArgParameter.ParameterType, _imports),
            };
            if (newParamType == null)
                return new(ErrorType.Unresolved, ins, $"Could not resolve parameter #{methodArgParameter.Index} {methodArgParameter.ParameterType} {methodArgParameter.Name}");

            var newParam = new ParameterDefinition(methodArgParameter.Name, methodArgParameter.Attributes,
                newParamType);
            newMethod.Parameters.Add(newParam);
        }

        _targetBuilder.Emit(ins.OpCode, _imports.Module.ImportReference(newMethod));
        return Result.OK;
    }

    private Result InlineType(Instruction ins)
    {
        var targetType = (TypeReference)ins.Operand;
        if (targetType is GenericParameter genericParam)
        {
            if (genericParam.Owner is TypeReference paramOwner)
            {
                var newTypeOwner =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, paramOwner, _imports);
                if (newTypeOwner == null)
                    return new(ErrorType.Unresolved, ins, $"Could not resolve owner type {paramOwner}");
                targetType = newTypeOwner.GenericParameters.Single(it => it.Name == targetType.Name);
            }
            else
            {
                targetType = _target.GenericParameters.Single(it => it.Name == targetType.Name);
            }
        }
        else
        {
            var oldTargetType = targetType;
            targetType = Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, targetType, _imports);
            if (targetType == null)
                return new(ErrorType.Unresolved, ins, $"Could not resolve type {oldTargetType}");
        }

        if (ins.OpCode == OpCodes.Castclass && !targetType.IsValueType)
        {
            _targetBuilder.Emit(OpCodes.Call,
                _imports.Module.ImportReference(new GenericInstanceMethod(_imports.Il2CppObjectBase_Cast.Value)
                { GenericArguments = { targetType } }));
            return Result.OK;
        }

        if (ins.OpCode == OpCodes.Isinst && !targetType.IsValueType)
        {
            _targetBuilder.Emit(OpCodes.Call,
                _imports.Module.ImportReference(new GenericInstanceMethod(_imports.Il2CppObjectBase_TryCast.Value)
                { GenericArguments = { targetType } }));
            return Result.OK;
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
            return Result.OK;
        }

        _targetBuilder.Emit(ins.OpCode, targetType);
        return Result.OK;
    }

    private Result InlineSig(Instruction ins)
    {
        // todo: rewrite sig if this ever happens in unity types
        return Result.Unimplemented(ins);
    }

    private Result InlineTok(Instruction ins)
    {
        var targetTok = ins.Operand as TypeReference;
        if (targetTok == null)
            return Result.Unimplemented(ins);

        if (targetTok is GenericParameter genericParam)
        {
            if (genericParam.Owner is TypeReference paramOwner)
            {
                var newTypeOwner =
                    Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, paramOwner, _imports);
                if (newTypeOwner == null)
                    return new(ErrorType.Unresolved, ins, $"Could not resolve owner type {paramOwner}");
                targetTok = newTypeOwner.GenericParameters.Single(it => it.Name == targetTok.Name);
            }
            else
            {
                targetTok = _target.GenericParameters.Single(it => it.Name == targetTok.Name);
            }
        }
        else
        {
            var oldTargetTok = targetTok;
            targetTok = Pass80UnstripMethods.ResolveTypeInNewAssemblies(_globalContext, targetTok, _imports);
            if (targetTok == null)
                return new(ErrorType.Unresolved, ins, $"Could not resolve type {oldTargetTok}");
        }

        _targetBuilder.Emit(OpCodes.Call,
            _imports.Module.ImportReference(
                new GenericInstanceMethod(_imports.Il2CppSystemRuntimeTypeHandleGetRuntimeTypeHandle.Value)
                { GenericArguments = { targetTok } }));
        return Result.OK;
    }

    private Result Copy(Instruction ins)
    {
        _targetBuilder.Append(ins);
        return Result.OK;
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

