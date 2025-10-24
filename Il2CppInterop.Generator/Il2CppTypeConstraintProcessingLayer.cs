using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class Il2CppTypeConstraintProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Il2CppType<T> Constraint Processor";
    public override string Id => "il2cpptype_constraint_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var iil2CppTypeGeneric = appContext.ResolveTypeOrThrow(typeof(IIl2CppType<>));
        var iobject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IObject");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                foreach (var genericParameter in type.GenericParameters)
                {
                    genericParameter.ConstraintTypes.Add(iil2CppTypeGeneric.MakeGenericInstanceType([genericParameter]));
                    genericParameter.ConstraintTypes.Add(iobject);
                }

                foreach (var method in type.Methods)
                {
                    if (method.IsInjected)
                        continue;

                    foreach (var genericParameter in method.GenericParameters)
                    {
                        genericParameter.ConstraintTypes.Add(iil2CppTypeGeneric.MakeGenericInstanceType([genericParameter]));
                        genericParameter.ConstraintTypes.Add(iobject);
                    }
                }
            }
        }
    }
}
