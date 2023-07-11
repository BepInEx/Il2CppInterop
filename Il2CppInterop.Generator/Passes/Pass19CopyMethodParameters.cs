using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass19CopyMethodParameters
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                foreach (var methodRewriteContext in typeContext.Methods)
                {
                    var originalMethod = methodRewriteContext.OriginalMethod;
                    var newMethod = methodRewriteContext.NewMethod;

                    foreach (var originalMethodParameter in originalMethod.Parameters)
                    {
                        var newName = originalMethodParameter.Name.IsObfuscated(context.Options)
                            ? $"param_{originalMethodParameter.Sequence}"
                            : originalMethodParameter.Name;

                        var newParameter = new ParameterDefinition(newName,
                            originalMethodParameter.Attributes & ~ParameterAttributes.HasFieldMarshal,
                            assemblyContext.RewriteTypeRef(originalMethodParameter.ParameterType, typeContext.isBoxedTypeVariant));

                        if (originalMethodParameter.IsParamsArray())
                        {
                            newParameter.Constant = null;
                            newParameter.IsOptional = true;
                        }
                        else
                            newParameter.Constant = originalMethodParameter.Constant;

                        newMethod.Parameters.Add(newParameter);
                    }

                    var paramsMethod = context.CreateParamsMethod(originalMethod, newMethod, assemblyContext.Imports,
                        type => assemblyContext.RewriteTypeRef(type, typeContext.isBoxedTypeVariant));
                    if (paramsMethod != null) typeContext.NewType.Methods.Add(paramsMethod);
                }
    }
}
