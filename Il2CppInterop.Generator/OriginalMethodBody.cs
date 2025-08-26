using System.Diagnostics.CodeAnalysis;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.PE.DotNet.Cil;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class OriginalMethodBody : MethodBodyBase
{
    public static bool MaybeStoreOriginalMethodBody(MethodDefinition originalMethod, MethodAnalysisContext methodContext)
    {
        if (TryResolveOriginalMethodBody(originalMethod, methodContext, out var originalMethodBody))
        {
            methodContext.PutExtraData(originalMethodBody);
            return true;
        }
        else
        {
            return false;
        }
    }

    private static bool TryResolveOriginalMethodBody(MethodDefinition method, MethodAnalysisContext methodContext, [NotNullWhen(true)] out OriginalMethodBody? originalMethodBody)
    {
        var body = method.CilMethodBody;
        if (body is null)
        {
            originalMethodBody = null;
            return false;
        }

        var resolver = new ContextResolver(methodContext);

        var originalInstructions = body.Instructions;
        originalInstructions.ExpandMacros();

        var newInstructions = new Instruction[originalInstructions.Count];
        for (var i = 0; i < newInstructions.Length; i++)
        {
            newInstructions[i] = new Instruction
            {
                Code = originalInstructions[i].OpCode,
            };
        }

        var newLocalVariables = new LocalVariable[body.LocalVariables.Count];
        for (var i = 0; i < newLocalVariables.Length; i++)
        {
            var localVariable = body.LocalVariables[i];
            var localVariableType = resolver.Resolve(localVariable.VariableType);
            if (localVariableType is null)
            {
                originalMethodBody = default;
                return false;
            }
            newLocalVariables[i] = new LocalVariable
            {
                Type = localVariableType,
            };
        }

        var newExceptionHandlers = new ExceptionHandler[body.ExceptionHandlers.Count];
        for (var i = 0; i < newExceptionHandlers.Length; i++)
        {
            var exceptionHandler = body.ExceptionHandlers[i];
            var handlerType = exceptionHandler.HandlerType;

            var tryStart = ResolveLabel(newInstructions, originalInstructions, exceptionHandler.TryStart);
            var tryEnd = ResolveLabel(newInstructions, originalInstructions, exceptionHandler.TryEnd);
            var handlerStart = ResolveLabel(newInstructions, originalInstructions, exceptionHandler.HandlerStart);
            var handlerEnd = ResolveLabel(newInstructions, originalInstructions, exceptionHandler.HandlerEnd);
            var filterStart = ResolveLabel(newInstructions, originalInstructions, exceptionHandler.FilterStart);
            TypeAnalysisContext? exceptionType;
            if (exceptionHandler.ExceptionType is null)
            {
                exceptionType = null;
            }
            else
            {
                exceptionType = resolver.Resolve(exceptionHandler.ExceptionType.ToTypeSignature());
                if (exceptionType is null)
                {
                    originalMethodBody = default;
                    return false;
                }
            }
            newExceptionHandlers[i] = new ExceptionHandler
            {
                HandlerType = handlerType,
                TryStart = tryStart,
                TryEnd = tryEnd,
                HandlerStart = handlerStart,
                HandlerEnd = handlerEnd,
                FilterStart = filterStart,
                ExceptionType = exceptionType,
            };
        }

        for (var i = 0; i < originalInstructions.Count; i++)
        {
            var instruction = originalInstructions[i];
            var operand = instruction.Operand;
            var resolved = operand switch
            {
                null or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or bool or char => operand,
                string @string => @string,
                AsmResolver.Utf8String utf8String => utf8String.ToString(),
                CilLocalVariable localVariable => newLocalVariables[localVariable.Index],
                Parameter parameter => parameter == method.Parameters.ThisParameter ? This.Instance : methodContext.Parameters[parameter.Index],
                ITypeDescriptor typeDescriptor => resolver.Resolve(typeDescriptor.ToTypeSignature()),
                IFieldDescriptor { Signature: not null } fieldDescriptor => resolver.Resolve(fieldDescriptor),
                IMethodDescriptor { Signature: not null } methodDescriptor => resolver.Resolve(methodDescriptor),
                ICilLabel label => ResolveLabel(newInstructions, originalInstructions, label),
                IReadOnlyList<ICilLabel> labels => ResolveOperand(labels, originalInstructions, newInstructions),
                StandAloneSignature => null,// Not currently supported
                _ => null,
            };
            if (resolved is null && operand is not null)
            {
                originalMethodBody = default;
                return false;
            }
            newInstructions[i].Operand = resolved;
        }

        originalMethodBody = new OriginalMethodBody
        {
            Instructions = newInstructions,
            LocalVariables = newLocalVariables,
            ExceptionHandlers = newExceptionHandlers,
        };
        return true;
    }

    private static ILabel[] ResolveOperand(IReadOnlyList<ICilLabel> labels, CilInstructionCollection originalInstructions, Instruction[] newInstructions)
    {
        var resolved = new ILabel[labels.Count];
        for (var i = 0; i < labels.Count; i++)
        {
            resolved[i] = ResolveLabel(newInstructions, originalInstructions, labels[i]);
        }
        return resolved;
    }

    [return: NotNullIfNotNull(nameof(label))]
    private static ILabel? ResolveLabel(Instruction[] newInstructions, CilInstructionCollection originalInstructions, ICilLabel? label) => label switch
    {
        null => null,
        CilInstructionLabel instructionLabel => instructionLabel.Instruction is not null
            ? newInstructions[originalInstructions.IndexOf(instructionLabel.Instruction)]
            : throw new ArgumentException("Instruction label must reference an instruction", nameof(label)),
        _ when label.GetType().Name == "CilEndLabel" => EndLabel.Instance,
        _ => throw new ArgumentException($"Label is an unsupported type: {label.GetType()}", nameof(label)),
    };
}
