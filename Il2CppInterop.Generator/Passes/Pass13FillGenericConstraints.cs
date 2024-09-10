using AsmResolver.DotNet;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass13FillGenericConstraints
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                for (var i = 0; i < typeContext.OriginalType.GenericParameters.Count; i++)
                {
                    var originalParameter = typeContext.OriginalType.GenericParameters[i];
                    var newParameter = typeContext.NewType.GenericParameters[i];
                    foreach (var originalConstraint in originalParameter.Constraints)
                    {
                        if (originalConstraint.IsSystemValueType() || originalConstraint.IsInterface())
                            continue;

                        if (originalConstraint.IsSystemEnum())
                        {
                            newParameter.Constraints.Add(new GenericParameterConstraint(
                                typeContext.AssemblyContext.Imports.Module.Enum().ToTypeDefOrRef()));
                            continue;
                        }

                        newParameter.Constraints.Add(
                            new GenericParameterConstraint(
                                assemblyContext.RewriteTypeRef(originalConstraint.Constraint!)));
                    }
                }
            }
        }
    }
}
