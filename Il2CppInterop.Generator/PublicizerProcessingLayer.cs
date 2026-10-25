using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class PublicizerProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "publicizer";
    public override string Name => "Publicizer";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
            {
                continue;
            }

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                {
                    continue;
                }

                type.Visibility = type.DeclaringType is null ? TypeAttributes.Public : TypeAttributes.NestedPublic;

                foreach (var field in type.Fields)
                {
                    if (!field.IsInjected)
                    {
                        field.Visibility = FieldAttributes.Public;
                    }
                }

                foreach (var method in type.Methods)
                {
                    if (method.Visibility != MethodAttributes.Public)
                    {
                        if (method.IsStaticConstructor || method.IsInjected)
                        {
                            continue;
                        }

                        if (method.ImplementsAnInterfaceMethod)
                        {
                            continue;
                        }

                        method.Visibility = MethodAttributes.Public;
                    }
                }
            }
        }
    }
}
