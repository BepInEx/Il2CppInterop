using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

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
        if (typeContext == null) return;
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

            var fieldType = originalField.FieldType;
            if (fieldType.IsPrimitive || fieldType.IsPointer) continue;
            if (fieldType.FullName == "System.String" || fieldType.FullName == "System.Object" || fieldType.IsArray ||
                fieldType.IsByReference || fieldType.IsGenericParameter || fieldType.IsGenericInstance)
            {
                typeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
                return;
            }

            var fieldTypeContext = typeContext.AssemblyContext.GlobalContext.GetNewTypeForOriginal(fieldType.Resolve());
            if (fieldTypeContext == null) return;
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
