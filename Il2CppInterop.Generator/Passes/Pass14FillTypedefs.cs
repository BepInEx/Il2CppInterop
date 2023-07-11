using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass14FillTypedefs
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                foreach (var originalParameter in typeContext.OriginalType.GenericParameters)
                {
                    var newParameter = new GenericParameter(originalParameter.Name, typeContext.NewType);
                    typeContext.NewType.GenericParameters.Add(newParameter);

                    var parameterSpecifics = typeContext.genericParameterUsage[originalParameter.Position];
                    if (parameterSpecifics == TypeRewriteContext.GenericParameterSpecifics.Strict ||
                        (parameterSpecifics == TypeRewriteContext.GenericParameterSpecifics.AffectsBlittability &&
                         typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct))
                    {
                        newParameter.Attributes = originalParameter.Attributes;
                        newParameter.MakeUnmanaged(assemblyContext);
                    }
                    else
                        newParameter.Attributes = originalParameter.Attributes.StripValueTypeConstraint();
                }

                if (typeContext.OriginalType.IsEnum)
                    typeContext.NewType.BaseType = assemblyContext.Imports.Module.Enum();
                else if (typeContext.ComputedTypeSpecifics.IsBlittable())
                    typeContext.NewType.BaseType = assemblyContext.Imports.Module.ValueType();
            }
        }

        // Second pass is explicitly done after first to account for rewriting of generic base types - value-typeness is important there
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                if (!typeContext.OriginalType.IsEnum && !typeContext.ComputedTypeSpecifics.IsBlittable())
                    typeContext.NewType.BaseType = assemblyContext.RewriteTypeRef(typeContext.OriginalType.BaseType, false);
    }
}
