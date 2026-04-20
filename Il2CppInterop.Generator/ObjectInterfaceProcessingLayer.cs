using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class ObjectInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Object Interface Processor";
    public override string Id => "object_interface_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // Find references
        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemValueType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");
        var il2CppSystemEnum = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");

        // Inject interfaces
        var il2CppSystemIObject = InjectInterface(appContext, "IObject");
        var il2CppSystemIValueType = InjectInterface(appContext, "IValueType");
        var il2CppSystemIEnum = InjectInterface(appContext, "IEnum");

        // Set KnownType
        il2CppSystemIObject.KnownType = KnownTypeCode.Il2CppSystem_IObject;
        il2CppSystemIValueType.KnownType = KnownTypeCode.Il2CppSystem_IValueType;
        il2CppSystemIEnum.KnownType = KnownTypeCode.Il2CppSystem_IEnum;

        // Il2CppSystem.IValueType : Il2CppSystem.IObject
        il2CppSystemIValueType.InterfaceContexts.Add(il2CppSystemIObject);

        // Il2CppSystem.IEnum : Il2CppSystem.IObject, Il2CppSystem.IValueType /* and the other interfaces that Il2CppSystem.Enum implements */
        il2CppSystemIEnum.InterfaceContexts.Add(il2CppSystemIObject);
        il2CppSystemIEnum.InterfaceContexts.Add(il2CppSystemIValueType);
        foreach (var interfaceContext in il2CppSystemEnum.InterfaceContexts)
        {
            il2CppSystemIEnum.InterfaceContexts.Add(interfaceContext);
        }

        // Il2CppSystem.ValueType : Il2CppSystem.IValueType
        il2CppSystemValueType.InterfaceContexts.Add(il2CppSystemIValueType);

        // Il2CppSystem.Enum : Il2CppSystem.IEnum
        il2CppSystemEnum.InterfaceContexts.Add(il2CppSystemIEnum);

        // Add methods to interfaces
        foreach (var method in il2CppSystemObject.Methods)
        {
            if (!method.IsConstructor && !method.IsStatic && !method.IsInjected)
            {
                CreateInterfaceMethod(method, il2CppSystemObject, il2CppSystemIObject);
            }
        }
        foreach (var method in il2CppSystemValueType.Methods)
        {
            if (!method.IsConstructor && !method.IsStatic && !method.IsInjected)
            {
                CreateInterfaceMethod(method, il2CppSystemValueType, il2CppSystemIValueType);
            }
        }
        foreach (var method in il2CppSystemEnum.Methods)
        {
            if (!method.IsConstructor && !method.IsStatic && !method.IsInjected)
            {
                CreateInterfaceMethod(method, il2CppSystemEnum, il2CppSystemIEnum);
            }
        }

        // Add interfaces to types
        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                type.InterfaceContexts.Add(il2CppSystemIObject);

                if (type.DefaultBaseType == il2CppSystemValueType)
                {
                    type.InterfaceContexts.Add(il2CppSystemIValueType);
                }
                else if (type.DefaultBaseType == il2CppSystemEnum)
                {
                    type.InterfaceContexts.Add(il2CppSystemIValueType);
                    type.InterfaceContexts.Add(il2CppSystemIEnum);
                }
            }
        }
    }

    private static InjectedTypeAnalysisContext InjectInterface(ApplicationAnalysisContext appContext, string name)
    {
        var result = appContext.Il2CppMscorlib.InjectType("Il2CppSystem", name, null, TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.Abstract);
        result.IsInjected = true;
        return result;
    }

    private static void CreateInterfaceMethod(MethodAnalysisContext classMethod, TypeAnalysisContext classType, TypeAnalysisContext interfaceType)
    {
        Debug.Assert(classMethod.DeclaringType == classType);
        Debug.Assert(classMethod.GenericParameters.Count == 0);
        Debug.Assert(classType.GenericParameters.Count == 0);
        Debug.Assert(interfaceType.IsInjected);

        var @explicitlyImplements = classMethod.DefaultName.Contains('.');

        var interfaceMethod = new InjectedMethodAnalysisContext(
            interfaceType,
            classMethod.DefaultName,
            classMethod.ReturnType,
            explicitlyImplements
                ? MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final
                : MethodAttributes.Public | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
            classMethod.Parameters.Select(p => p.ParameterType).ToArray(),
            classMethod.Parameters.Select(p => p.Name).ToArray(),
            classMethod.Parameters.Select(p => p.Attributes).ToArray())
        {
            OverrideName = classMethod.OverrideName,
        };
        interfaceType.Methods.Add(interfaceMethod);

        foreach (var @override in classMethod.Overrides)
        {
            if (@override.DeclaringType is not { IsInterface: true })
            {
                continue;
            }
            if (@explicitlyImplements)
            {
                interfaceMethod.Overrides.Add(@override);
                @explicitlyImplements = true;
                continue;
            }

            var overrideImplementation = new InjectedMethodAnalysisContext(
                interfaceType,
                $"{@override.DeclaringType!.FullName}.{interfaceMethod.Name}",
                classMethod.ReturnType,
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.HideBySig | MethodAttributes.Final,
                classMethod.Parameters.Select(p => p.ParameterType).ToArray(),
                classMethod.Parameters.Select(p => p.Name).ToArray(),
                classMethod.Parameters.Select(p => p.Attributes).ToArray())
            {
                OverrideName = classMethod.OverrideName,
                IsInjected = true,
            };
            interfaceType.Methods.Add(overrideImplementation);
            overrideImplementation.Overrides.Add(@override);

            List<Instruction> instructions =
            [
                new Instruction(CilOpCodes.Ldarg_0),
                ..Enumerable.Range(0, classMethod.Parameters.Count).Select(i => new Instruction
                {
                    Code = CilOpCodes.Ldarg,
                    Operand = overrideImplementation.Parameters[i],
                }),
                new Instruction(CilOpCodes.Callvirt, interfaceMethod),
                new Instruction(CilOpCodes.Ret),
            ];
            overrideImplementation.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions,
            });
        }

        classMethod.InterfaceRedirectMethod = interfaceMethod;
        if (!classMethod.IsVirtual)
        {
            // If the class method isn't virtual, we want to make sure it can't be overridden in subclasses.
            classMethod.Attributes |= MethodAttributes.Final | MethodAttributes.NewSlot;
        }
        classMethod.Attributes |= MethodAttributes.Virtual | MethodAttributes.HideBySig;
    }
}
