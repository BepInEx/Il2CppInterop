using AsmResolver.DotNet;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass11ComputeGenericParameterSpecifics
{
    private static RewriteGlobalContext globalContext;

    public static void DoPass(RewriteGlobalContext context)
    {
        globalContext = context;

        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ComputeGenericParameterUsageSpecifics(typeContext);
    }

    private static void ComputeGenericParameterUsageSpecifics(TypeRewriteContext typeContext)
    {
        if (typeContext.genericParameterUsageComputed) return;
        typeContext.genericParameterUsageComputed = true;

        var originalType = typeContext.OriginalType;
        if (originalType.GenericParameters.Count == 0) return;

        void OnResult(GenericParameter parameter, TypeRewriteContext.GenericParameterSpecifics specific)
        {
            if (parameter.Owner != originalType) return;

            typeContext.SetGenericParameterUsageSpecifics(parameter.Number, specific);
        }

        foreach (var originalField in originalType.Fields)
        {
            // Sometimes il2cpp metadata has invalid field offsets for some reason (https://github.com/SamboyCoding/Cpp2IL/issues/167)
            if (originalField.ExtractFieldOffset() >= 0x8000000) continue;
            if (originalField.IsStatic) continue;

            FindTypeGenericParameters(originalField.Signature!.FieldType, originalType.GetGenericParameterContext(),
                TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability, OnResult);
        }

        foreach (var originalField in originalType.Fields)
        {
            // Sometimes il2cpp metadata has invalid field offsets for some reason (https://github.com/SamboyCoding/Cpp2IL/issues/167)
            if (originalField.ExtractFieldOffset() >= 0x8000000) continue;
            if (!originalField.IsStatic) continue;

            FindTypeGenericParameters(originalField.Signature!.FieldType, originalType.GetGenericParameterContext(),
                TypeRewriteContext.GenericParameterSpecifics.Relaxed, OnResult);
        }

        foreach (var originalMethod in originalType.Methods)
        {
            FindTypeGenericParameters(originalMethod.Signature!.ReturnType, originalMethod.GetGenericParameterContext(),
                TypeRewriteContext.GenericParameterSpecifics.Relaxed, OnResult);

            foreach (Parameter parameter in originalMethod.Parameters)
            {
                FindTypeGenericParameters(parameter.ParameterType, originalMethod.GetGenericParameterContext(),
                    TypeRewriteContext.GenericParameterSpecifics.Relaxed, OnResult);
            }
        }

        foreach (TypeDefinition nestedType in originalType.NestedTypes)
        {
            var nestedContext = globalContext.GetNewTypeForOriginal(nestedType);
            ComputeGenericParameterUsageSpecifics(nestedContext);

            foreach (var parameter in nestedType.GenericParameters)
            {
                var myParameter = originalType.GenericParameters
                    .FirstOrDefault(param => param.Name.Equals(parameter.Name));

                if (myParameter == null) continue;

                var otherParameterSpecific = nestedContext.genericParameterUsage[parameter.Number];
                if (otherParameterSpecific == TypeRewriteContext.GenericParameterSpecifics.Strict)
                    typeContext.SetGenericParameterUsageSpecifics(myParameter.Number, otherParameterSpecific);
            }
        }
    }

    private static void FindTypeGenericParameters(
        TypeSignature? reference,
        GenericParameterContext parameterContext,
        TypeRewriteContext.GenericParameterSpecifics currentConstraint,
        Action<GenericParameter, TypeRewriteContext.GenericParameterSpecifics> onFound)
    {
        if (reference is GenericParameterSignature parameterSignature)
        {
            var genericParameter = parameterContext.GetGenericParameter(parameterSignature);
            onFound?.Invoke(genericParameter!, currentConstraint);
            return;
        }

        if (reference is PointerTypeSignature)
        {
            FindTypeGenericParameters(reference!.GetElementType(), parameterContext,
                TypeRewriteContext.GenericParameterSpecifics.Strict, onFound);
            return;
        }

        if (reference is ArrayBaseTypeSignature or ByReferenceTypeSignature)
        {
            FindTypeGenericParameters(reference.GetElementType(), parameterContext,
                currentConstraint, onFound);
            return;
        }

        if (reference is GenericInstanceTypeSignature genericInstance)
        {
            var typeDefinition = reference.Resolve()!;
            var typeContext = globalContext.GetNewTypeForOriginal(typeDefinition);
            ComputeGenericParameterUsageSpecifics(typeContext);
            for (var i = 0; i < genericInstance.TypeArguments.Count; i++)
            {
                var myConstraint = typeContext.genericParameterUsage[i];
                if (myConstraint == TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability)
                    myConstraint = currentConstraint;

                var genericArgument = genericInstance.TypeArguments[i];
                FindTypeGenericParameters(genericArgument, parameterContext, myConstraint, onFound);
            }
        }
    }
}
