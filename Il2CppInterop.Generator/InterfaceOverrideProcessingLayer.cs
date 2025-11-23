using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Utils;
using Il2CppInterop.Generator.Operands;
using LibCpp2IL;

namespace Il2CppInterop.Generator;

public class InterfaceOverrideProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Interface Override Renaming";
    public override string Id => "interfaceoverrides";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // Note: set item equality is currently reference equality, which is not ideal here but works for now.
        HashSet<(MethodAnalysisContext ImplementingMethod, MethodAnalysisContext InterfaceMethod)> set = new();
        List<(TypeAnalysisContext Type, MethodAnalysisContext ImplementingMethod, MethodAnalysisContext InterfaceMethod)> list = new();
        foreach (var type in appContext.AllTypes)
        {
            if (type.IsInterface)
            {
                continue;
            }

            var definition = type.Definition;
            if (definition == null)
                continue;
            var vtable = definition.VTable;

            for (var index = 0; index < vtable.Length; index++)
            {
                var vtableEntry = vtable[index];
                var interfaceMethod = GetImplementedMethod(type, definition, index);
                if (interfaceMethod == null)
                    continue;

                var implementingMethod = vtableEntry?.Type switch
                {
                    MetadataUsageType.MethodDef => appContext.ResolveContextForMethod(vtableEntry.AsMethod()),
                    MetadataUsageType.MethodRef => appContext.ResolveContextForMethod(vtableEntry.AsGenericMethodRef()),
                    _ => null
                };
                if (implementingMethod == null)
                    continue;

                if (implementingMethod.DeclaringType is null or { IsInterface: false })
                    continue;

                if (implementingMethod.Name == interfaceMethod.Name)
                    continue;

                /*if (interfaceMethod is not ConcreteGenericMethodAnalysisContext)
                {
                    var baseImplementingMethod = (implementingMethod as ConcreteGenericMethodAnalysisContext)?.BaseMethodContext ?? implementingMethod;
                    set.Add((baseImplementingMethod, interfaceMethod));
                }
                else if (implementingMethod is not ConcreteGenericMethodAnalysisContext)
                {
                    set.Add((implementingMethod, interfaceMethod));
                }
                else*/
                {
                    // Complex case - both are concrete generic methods.
                    list.Add((type, implementingMethod, interfaceMethod));
                }
            }
        }

        foreach ((var implementingMethod, var interfaceMethod) in set)
        {
            // Again, this is reference equality, not something more robust.
            //if (!implementingMethod.Overrides.Contains(interfaceMethod))
            {
                //implementingMethod.Overrides.Add(interfaceMethod);
            }
        }

        foreach (var (type, implementingMethod, interfaceMethod) in list)
        {
            var methodName = $"{interfaceMethod.DeclaringType?.FullName}.{interfaceMethod.Name}";
            var newMethod = new InjectedMethodAnalysisContext(
                type,
                methodName,
                implementingMethod.ReturnType,
                implementingMethod.Attributes,
                implementingMethod.Parameters.Select(p => p.ParameterType).ToArray(),
                implementingMethod.Parameters.Select(p => p.Name).ToArray(),
                implementingMethod.Parameters.Select(p => p.Attributes).ToArray(),
                implementingMethod.ImplAttributes)
            {
                IsInjected = true
            };
            type.Methods.Add(newMethod);

            newMethod.OverridesList.Add(interfaceMethod);

            var instructions = new List<Instruction>
            {
                { CilOpCodes.Ldarg, This.Instance }
            };
            foreach (var param in newMethod.Parameters)
            {
                instructions.Add(CilOpCodes.Ldarg, param);
            }
            instructions.Add(CilOpCodes.Call, implementingMethod);
            instructions.Add(CilOpCodes.Ret);

            newMethod.PutExtraData(new NativeMethodBody() { Instructions = instructions });
        }

        foreach ((_, var implementingMethod, _) in list)
        {
            implementingMethod.OverrideAttributes = implementingMethod.Attributes & ~(MethodAttributes.Final | MethodAttributes.Virtual);
            implementingMethod.Visibility = MethodAttributes.Public;
        }
    }

    private static MethodAnalysisContext? GetImplementedMethod(TypeAnalysisContext type, LibCpp2IL.Metadata.Il2CppTypeDefinition definition, int index)
    {
        foreach (var interfaceOffset in definition.InterfaceOffsets)
        {
            if (index >= interfaceOffset.offset)
            {
                var interfaceTypeContext = interfaceOffset.Type.ToContext(type.DeclaringAssembly);
                if (interfaceTypeContext != null && interfaceTypeContext.TryGetMethodInSlot(index - interfaceOffset.offset, out var method))
                {
                    return method;
                }
            }
        }
        return null;
    }
}
