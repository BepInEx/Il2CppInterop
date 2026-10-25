using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

/// <summary>
/// Poorly implemented obfuscation can cause method names to differ between base and derived methods, which causes issues for virtual method dispatch if not corrected.
/// </summary>
public class OverrideRenamingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Override Name Changes";
    public override string Id => "overridenamechanges";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;
            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;
                RenameMembers(type);
            }
        }
    }
    private static void RenameMembers(TypeAnalysisContext type)
    {
        foreach (var method in type.Methods)
        {
            if (method.IsInjected)
                continue;
            var baseMethod = method.UltimateBaseMethod;
            if (baseMethod is not null && baseMethod.DefaultName != method.DefaultName)
            {
                method.Name = baseMethod.Name;
            }
        }
    }
}
