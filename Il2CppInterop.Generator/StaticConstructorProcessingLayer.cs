using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class StaticConstructorProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Static Constructor Processor";
    public override string Id => "static_constructor_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        foreach (var type in appContext.AllTypes)
        {
            var instructions = type.StaticConstructorInstructions;
            if (instructions is null or { Count: 0 })
                continue;

            for (var i = type.Methods.Count - 1; i >= 0; i--)
            {
                if (type.Methods[i].IsStaticConstructor)
                {
                    Debug.Fail($"Type {type.FullName} already has a static constructor defined. It should have been renamed in an earlier processing layer.");
                }
            }

            // Add a final instruction to return from the static constructor.
            instructions.Add(new Instruction(OpCodes.Ret));

            var staticConstructor = new InjectedMethodAnalysisContext(
                type,
                ".cctor",
                type.AppContext.SystemTypes.SystemVoidType,
                MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                [])
            {
                IsInjected = true
            };
            type.Methods.Add(staticConstructor);

            var nativeMethodBody = new NativeMethodBody()
            {
                Instructions = instructions,
            };
            staticConstructor.PutExtraData(nativeMethodBody);
        }
    }
}
