using AsmResolver.DotNet;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.OutputFormats;

namespace Il2CppInterop.Generator;

public class AsmResolverDllOutputFormatUnstripped : AsmResolverDllOutputFormatThrowNull
{
    public override string OutputFormatId => "dll_unstripped";

    public override string OutputFormatName => "DLL files with method bodies containing an unstripped implementation if available.";

    protected override void FillMethodBody(MethodDefinition methodDefinition, MethodAnalysisContext methodContext)
    {
        if (methodContext.TryGetExtraData(out OriginalMethodBody? originalBody))
        {
            originalBody.FillMethodBody(methodDefinition);
        }
        else
        {
            base.FillMethodBody(methodDefinition, methodContext);
        }
    }
}
