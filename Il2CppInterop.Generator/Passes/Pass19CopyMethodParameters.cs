using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Passes;

public static class Pass19CopyMethodParameters
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                foreach (var methodRewriteContext in typeContext.Methods)
                {
                    var originalMethod = methodRewriteContext.OriginalMethod;
                    var newMethod = methodRewriteContext.NewMethod;

                    foreach (var originalMethodParameter in originalMethod.Parameters)
                    {
                        var newName = originalMethodParameter.Name.IsObfuscated(context.Options)
                            ? $"param_{originalMethodParameter.Sequence}"
                            : originalMethodParameter.Name;

                        var newParameter = newMethod.AddParameter(
                            assemblyContext.RewriteTypeRef(originalMethodParameter.ParameterType),
                            newName,
                            originalMethodParameter.GetOrCreateDefinition().Attributes & ~ParameterAttributes.HasFieldMarshal);

                        if (originalMethodParameter.IsParamsArray())
                        {
                            newParameter.Definition!.Constant = null;
                            newParameter.Definition.IsOptional = true;
                        }
                        else
                        {
                            newParameter.Definition!.Constant = originalMethodParameter.Definition!.Constant;
                        }
                    }

                    var paramsMethod = context.CreateParamsMethod(originalMethod, newMethod, assemblyContext.Imports,
                        type => assemblyContext.RewriteTypeRef(type));
                    if (paramsMethod != null) typeContext.NewType.Methods.Add(paramsMethod);
                }
            }
        }
    }
}
