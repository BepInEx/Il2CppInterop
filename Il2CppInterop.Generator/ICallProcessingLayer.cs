using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class ICallProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "ICall Processor";
    public override string Id => "icall_processor";
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
                    if (!method.IsUnstripped || !method.DefaultImplAttributes.HasFlag(MethodImplAttributes.InternalCall))
                        continue;

                    Debug.Assert(type.InitializationType is not null);
                    Debug.Assert(!method.HasExtraData<OriginalMethodBody>());
                    Debug.Assert(!method.HasExtraData<TranslatedMethodBody>());
                    Debug.Assert(!method.HasExtraData<NativeMethodBody>());
                    Debug.Assert(method.GenericParameters.Count == 0 && type.GenericParameters.Count == 0, "Internal calls cannot be generic.");

                }
            }
        }
    }

    private static string GetICallSignature(MethodAnalysisContext method)
    {
        return $"{method.DeclaringType!.DefaultFullName}::{method.DefaultName}";
    }
}
