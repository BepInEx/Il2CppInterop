using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class InvisibleInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Invisible Interface Processor";
    public override string Id => "invisible_interface_processor";
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

                foreach (var method in type.Methods)
                {
                    foreach (var overriddenMethod in method.Overrides)
                    {
                        if (overriddenMethod.DeclaringType is not { IsInterface: true } @interface)
                        {
                            continue;
                        }

                        if (!type.ImplementsInterface(@interface))
                        {
                            type.InterfaceContexts.Add(@interface);
                        }
                    }
                }
            }
        }
    }
}
