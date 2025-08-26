using System.Diagnostics;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class ConstantInitializationProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Constant Initialization Processor";
    public override string Id => "constant_initialization_processor";
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

                foreach (var field in type.Fields)
                {
                    if (field.ConstantValue is null || field.IsInjected)
                        continue;

                    Debug.Assert(field.IsStatic);

                    var instructions = type.GetOrCreateStaticConstructorInstructions();

                    object operandCast;
                    unchecked
                    {
                        operandCast = field.ConstantValue switch
                        {
                            bool value => value ? 1 : 0,
                            char value => (int)value,
                            byte value => (int)value,
                            sbyte value => (int)value,
                            ushort value => (int)value,
                            short value => (int)value,
                            uint value => (int)value,
                            ulong value => (long)value,
                            _ => field.ConstantValue,
                        };
                    }

                    var opCode = operandCast switch
                    {
                        long => OpCodes.Ldc_I8,
                        float => OpCodes.Ldc_R4,
                        double => OpCodes.Ldc_R8,
                        string => OpCodes.Ldstr,
                        _ => OpCodes.Ldc_I4
                    };

                    instructions.Add(new Instruction(opCode, operandCast));
                    if (opCode == OpCodes.Ldstr)
                    {
                        MonoIl2CppConversion.AddMonoToIl2CppStringConversion(instructions, appContext);
                    }
                    else
                    {
                        MonoIl2CppConversion.AddMonoToIl2CppConversion(instructions, field.FieldType);
                    }
                    instructions.Add(new Instruction(OpCodes.Stsfld, field));
                }
            }
        }
    }
}
