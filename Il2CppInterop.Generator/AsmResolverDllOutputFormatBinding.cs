using AsmResolver.DotNet;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.OutputFormats;

namespace Il2CppInterop.Generator;

public class AsmResolverDllOutputFormatBinding : AsmResolverDllOutputFormatThrowNull
{
    public override string OutputFormatId => "dll_binding";

    public override string OutputFormatName => "DLL files with method bodies containing a binding for modding.";

    protected override void FillMethodBody(MethodDefinition methodDefinition, MethodAnalysisContext methodContext)
    {
        if (methodContext.RuntimeImplemented)
        {
            // This is a runtime-implemented method, so we don't need to do anything.
        }
        else if (methodContext.TryGetExtraData(out TranslatedMethodBody? translatedBody))
        {
            translatedBody.FillMethodBody(methodDefinition);
        }
        else if (methodContext.TryGetExtraData(out NativeMethodBody? nativeBody))
        {
            nativeBody.FillMethodBody(methodDefinition);
        }
        else
        {
            // This gets called when the body of an unstripped method could not be translated.
            // We could have it throw a custom exception with a message, but throwing null is sufficient for now.
            base.FillMethodBody(methodDefinition, methodContext);
        }
    }

    public override List<AssemblyDefinition> BuildAssemblies(ApplicationAnalysisContext context)
    {
        var list = base.BuildAssemblies(context);

        // Todo: Remove injected reference assemblies from the output

        // Todo: Replace mscorlib references with .NET Core references

        return list;
    }
}
