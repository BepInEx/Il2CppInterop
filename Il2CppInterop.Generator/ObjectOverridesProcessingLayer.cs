using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class ObjectOverridesProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Object Overrides Processor";
    public override string Id => "object_overrides_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var systemObject = appContext.SystemTypes.SystemObjectType;
        var il2CppSystemIObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IObject");

        ImplementVirtualMethodsOnIl2CppSystemObject(appContext, systemObject, il2CppSystemIObject);
        ImplementVirtualMethodsOnStructsAndEnums(appContext, systemObject, il2CppSystemIObject);
    }

    private static void ImplementVirtualMethodsOnIl2CppSystemObject(ApplicationAnalysisContext appContext, TypeAnalysisContext systemObject, TypeAnalysisContext il2CppSystemIObject)
    {
        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");

        (MethodAnalysisContext, MethodAnalysisContext)[] virtualMethods = systemObject.Methods
            .Where(m => m.IsVirtual && m.DefaultName is not "Finalize")
            .Select(m =>
            (
                m,
                il2CppSystemObject.Methods.Single(i => i.DefaultName == m.DefaultName && !i.IsStatic)
            ))
            .ToArray();

        ImplementVirtualMethods(systemObject, il2CppSystemIObject, virtualMethods, il2CppSystemObject, false);
    }

    private static void ImplementVirtualMethodsOnStructsAndEnums(ApplicationAnalysisContext appContext, TypeAnalysisContext systemObject, TypeAnalysisContext il2CppSystemIObject)
    {
        var il2CppSystemValueType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");
        var il2CppSystemEnum = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");

        (MethodAnalysisContext, MethodAnalysisContext)[] virtualMethods = systemObject.Methods
            .Where(m => m.IsVirtual && m.DefaultName is not "Finalize")
            .Select(m =>
            (
                m,
                il2CppSystemIObject.Methods.Single(i => i.DefaultName == m.DefaultName)
            ))
            .ToArray();

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.DefaultBaseType != il2CppSystemEnum && type.DefaultBaseType != il2CppSystemValueType)
                    continue;

                if (type == il2CppSystemEnum)
                    continue;

                if (type.IsInjected)
                    continue;
                ImplementVirtualMethods(systemObject, il2CppSystemIObject, virtualMethods, type, true);
            }
        }
    }

    private static void ImplementVirtualMethods(TypeAnalysisContext systemObject, TypeAnalysisContext il2CppSystemIObject, (MethodAnalysisContext, MethodAnalysisContext)[] virtualMethods, TypeAnalysisContext type, bool box)
    {
        foreach ((var systemMethod, var il2CppMethod) in virtualMethods)
        {
            var newMethod = new InjectedMethodAnalysisContext(
                type,
                systemMethod.DefaultName,
                systemMethod.ReturnType,
                MethodAttributes.Public | MethodAttributes.Final | MethodAttributes.Virtual | MethodAttributes.HideBySig,
                systemMethod.Parameters.Select(p => p.ParameterType).ToArray(),
                systemMethod.Parameters.Select(p => p.Name).ToArray(),
                systemMethod.Parameters.Select(p => p.Attributes).ToArray())
            {
                IsInjected = true,
            };
            type.Methods.Add(newMethod);

            List<Instruction> instructions =
            [
                new Instruction(CilOpCodes.Ldarg_0),
            ];
            if (box)
            {
                var instantiatedType = type.SelfInstantiateIfGeneric();
                instructions.Add(CilOpCodes.Ldobj, instantiatedType);
                instructions.Add(CilOpCodes.Box, instantiatedType);
            }
            for (var i = 0; i < newMethod.Parameters.Count; i++)
            {
                var parameter = newMethod.Parameters[i];
                instructions.Add(CilOpCodes.Ldarg, parameter);
                if (parameter.ParameterType == systemObject)
                {
                    Debug.Assert(il2CppMethod.Parameters[i].ParameterType == il2CppSystemIObject);
                    instructions.Add(CilOpCodes.Isinst, il2CppSystemIObject);
                }
            }
            instructions.Add(CilOpCodes.Callvirt, il2CppMethod);
            instructions.Add(CilOpCodes.Call, il2CppMethod.ReturnType.GetImplicitConversionTo(newMethod.ReturnType));
            instructions.Add(CilOpCodes.Ret);

            newMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions
            });
        }
    }
}
