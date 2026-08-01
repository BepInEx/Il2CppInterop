using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass11ComputeTypeSpecifics
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                ComputeSpecifics(typeContext);
    }

    private static void ComputeSpecifics(TypeRewriteContext typeContext)
    {
        if (typeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.NotComputed) return;
        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.Computing;

        foreach (var originalField in typeContext.OriginalType.Fields)
        {
            // Sometimes il2cpp metadata has invalid field offsets for some reason (https://github.com/SamboyCoding/Cpp2IL/issues/167)
            if (originalField.ExtractFieldOffset() >= 0x8000000)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            if (originalField.IsStatic) continue;

            var fieldType = originalField.Signature!.FieldType;
            if (fieldType.IsPrimitive() || fieldType is PointerTypeSignature)
                continue;
            if (fieldType.FullName == "System.String" || fieldType.FullName == "System.Object"
                || fieldType is ArrayBaseTypeSignature or ByReferenceTypeSignature or GenericParameterSignature or GenericInstanceTypeSignature)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            // A field's type is not always resolvable, and even when it is, it
            // may belong to an assembly that is not part of this rewrite (for
            // example a type Cpp2IL referenced but did not emit). Both cases
            // previously produced a null context that was dereferenced on the
            // recursive call. Treat either as non-blittable, which is the safe
            // direction: it only costs the slower marshalling path, whereas
            // wrongly reporting blittable would corrupt memory at runtime.
            var resolvedFieldType = fieldType.Resolve();
            if (resolvedFieldType == null)
            {
                Logger.Instance.LogTrace(
                    "Field {FieldType} {TypeName}.{FieldName} could not be resolved; treating the declaring type as non-blittable",
                    fieldType.FullName, typeContext.OriginalType.FullName, originalField.Name?.ToString());

                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            var fieldTypeContext = typeContext.AssemblyContext.GlobalContext.TryGetNewTypeForOriginal(resolvedFieldType);
            if (fieldTypeContext == null)
            {
                Logger.Instance.LogTrace(
                    "Field type {FieldType} of {TypeName}.{FieldName} is not part of this rewrite; treating the declaring type as non-blittable",
                    resolvedFieldType.FullName, typeContext.OriginalType.FullName, originalField.Name?.ToString());

                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            ComputeSpecifics(fieldTypeContext);
            if (fieldTypeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.BlittableStruct)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }
        }

        typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.BlittableStruct;
    }
}
