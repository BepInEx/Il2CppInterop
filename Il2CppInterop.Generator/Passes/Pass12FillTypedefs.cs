using AsmResolver.DotNet;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass12FillTypedefs
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                foreach (var originalParameter in typeContext.OriginalType.GenericParameters)
                {
                    var newParameter = new GenericParameter(originalParameter.Name.MakeValidInSource(),
                        originalParameter.Attributes.StripValueTypeConstraint());
                    typeContext.NewType.GenericParameters.Add(newParameter);
                }

                if (typeContext.OriginalType.IsEnum)
                    typeContext.NewType.BaseType = assemblyContext.Imports.Module.Enum().ToTypeDefOrRef();
                else if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.BlittableStruct)
                    typeContext.NewType.BaseType = assemblyContext.Imports.Module.ValueType().ToTypeDefOrRef();
            }

        // Second pass is explicitly done after first to account for rewriting of generic base types - value-typeness is important there
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                if (!typeContext.OriginalType.IsEnum && typeContext.ComputedTypeSpecifics !=
                    TypeRewriteContext.TypeSpecifics.BlittableStruct)
                    typeContext.NewType.BaseType = assemblyContext.RewriteTypeRef(typeContext.OriginalType.BaseType!);
    }
}
