using Il2CppInterop.Generator.Contexts;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass13CreateGenericNonBlittableTypes
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.OriginalTypes)
            {
                if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
                    CreateBoxedType(typeContext);
            }
    }

    private static void CreateBoxedType(TypeRewriteContext typeContext, TypeDefinition parentType = null)
    {
        AssemblyRewriteContext assemblyContext = typeContext.AssemblyContext;
        var typeName = typeContext.NewType.Name;
        // Append _unboxed to blittable type for compatibility
        typeContext.NewType.Name = GetUnboxedName(typeName);


        TypeDefinition newBoxedType = new TypeDefinition(
            typeContext.NewType.Namespace,
            typeName,
            typeContext.NewType.Attributes);

        var declaringType = parentType ?? typeContext.NewType.DeclaringType;
        if (declaringType == null)
        {
            assemblyContext.NewAssembly.MainModule.Types.Add(newBoxedType);
        }
        else
        {
            declaringType.NestedTypes.Add(newBoxedType);
            newBoxedType.DeclaringType = declaringType;
        }

        TypeRewriteContext boxedTypeContext = new TypeRewriteContext(assemblyContext, typeContext.OriginalType, newBoxedType);
        boxedTypeContext.ComputedTypeSpecifics = TypeRewriteContext.TypeSpecifics.NonBlittableStruct;
        boxedTypeContext.isBoxedTypeVariant = true;
        typeContext.BoxedTypeContext = boxedTypeContext;
        assemblyContext.RegisterTypeContext(boxedTypeContext);

        foreach (TypeDefinition nestedType in typeContext.OriginalType.NestedTypes)
        {
            var nestedContext = assemblyContext.GetContextForOriginalType(nestedType);
            CreateBoxedType(nestedContext, newBoxedType);
        }
    }

    internal static string GetUnboxedName(string originalName)
    {
        var parts = originalName.Split('`');

        if (parts.Length == 2)
            return $"{parts[0]}_Unboxed`{parts[1]}";

        return $"{parts[0]}_Unboxed";
    }
}
