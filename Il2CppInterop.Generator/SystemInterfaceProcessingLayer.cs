using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Conversions;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Generator.Visitors;

namespace Il2CppInterop.Generator;

/// <summary>
/// This processing layer finds matching interfaces in mscorlib and Il2Cppmscorlib and implements
/// the mscorlib interfaces on the Il2Cppmscorlib interfaces, forwarding calls to the existing Il2Cppmscorlib methods.
/// This allows user code to use the normal .NET interfaces and have them work with the Il2Cpp types.
/// </summary>
public sealed class SystemInterfaceProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "System Interface Implementations";
    public override string Id => "system_interface_implementations";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // The reason we inject some new interfaces instead of just adding methods to the existing Il2Cpp interfaces is partially because
        // `Il2CppSystem.Runtime.CompilerServices.ICriticalNotifyCompletion` does not always inherit from
        // `Il2CppSystem.Runtime.CompilerServices.INotifyCompletion`, even though `System.Runtime.CompilerServices.ICriticalNotifyCompletion`
        // always inherits from `System.Runtime.CompilerServices.INotifyCompletion`.
        // Without these injected interfaces, types that implement `Il2CppSystem.Runtime.CompilerServices.ICriticalNotifyCompletion`
        // cause a TypeLoadException if they do not also implement `Il2CppSystem.Runtime.CompilerServices.INotifyCompletion`.
        // The TypeLoadException would come from `System.Runtime.CompilerServices.INotifyCompletion::OnCompleted` not being implemented.

        // Injected interfaces circumvent this issue by:
        // * Recreating the entire interface hierarchy of the system interfaces in a new namespace (`Il2CppInterop.SystemInterfaces`)
        // * Implementing the system interfaces on the injected interfaces
        // * Making the Il2Cpp interfaces inherit from the injected interfaces instead of the system interfaces

        var pairs = FindPairs(appContext);

        // Create interfaces in Il2CppInterop.SystemInterfaces
        var systemToInjectedMap = new Dictionary<TypeAnalysisContext, TypeAnalysisContext>(TypeAnalysisContextEqualityComparer.Instance);
        foreach ((_, var systemInterface) in pairs)
        {
            var injectedInterface = CreateInjectedInterface(systemInterface);
            AddPairToMap(systemToInjectedMap, systemInterface, injectedInterface);
        }

        // Find any missing base interfaces
        {
            for (var i = 0; i < pairs.Count; i++)
            {
                (_, var systemInterface) = pairs[i];
                foreach (var systemBaseInterface in systemInterface.InterfaceContexts.Select(RemoveTypeArgumentsIfPresent))
                {
                    if (!systemToInjectedMap.ContainsKey(systemBaseInterface))
                    {
                        var injectedBaseInterface = CreateInjectedInterface(systemBaseInterface);
                        AddPairToMap(systemToInjectedMap, systemBaseInterface, injectedBaseInterface);
                        pairs.Add((null, systemBaseInterface));
                    }
                }
            }
        }

        // Assign inheritance to the injected interfaces
        {
            var visitor = new TypeReplacementVisitor(systemToInjectedMap);
            foreach ((_, var systemInterface) in pairs)
            {
                var injectedInterface = systemToInjectedMap[systemInterface];
                foreach (var systemBaseInterface in systemInterface.InterfaceContexts)
                {
                    injectedInterface.InterfaceContexts.Add(visitor.Replace(systemBaseInterface));
                }
            }
        }

        foreach (var (il2CppInterface, systemInterface) in pairs)
        {
            ImplementInterface(il2CppInterface, systemToInjectedMap[systemInterface], systemInterface);
        }
    }

    private static TypeAnalysisContext RemoveTypeArgumentsIfPresent(TypeAnalysisContext type) => type is GenericInstanceTypeAnalysisContext genericInstance
        ? genericInstance.GenericType
        : type;

    private static InjectedTypeAnalysisContext CreateInjectedInterface(TypeAnalysisContext systemInterface)
    {
        var injectedInterface = systemInterface.AppContext.Il2CppMscorlib.InjectType(
            "Il2CppInterop.SystemInterfaces",
            systemInterface.Name,
            null,
            TypeAttributes.NotPublic | TypeAttributes.Interface | TypeAttributes.Abstract);
        injectedInterface.CopyGenericParameters(systemInterface, true);
        injectedInterface.InterfaceContexts.Add(systemInterface.MaybeMakeGenericInstanceType(injectedInterface.GenericParameters));
        return injectedInterface;
    }

    private static void AddPairToMap(Dictionary<TypeAnalysisContext, TypeAnalysisContext> systemToInjectedMap, TypeAnalysisContext systemInterface, InjectedTypeAnalysisContext injectedInterface)
    {
        systemToInjectedMap[systemInterface] = injectedInterface;

        // Add generic parameters to the dictionary too
        for (var i = 0; i < systemInterface.GenericParameters.Count; i++)
        {
            systemToInjectedMap[systemInterface.GenericParameters[i]] = injectedInterface.GenericParameters[i];
        }
    }

    private static List<(TypeAnalysisContext?, TypeAnalysisContext)> FindPairs(ApplicationAnalysisContext appContext)
    {
        List<(TypeAnalysisContext?, TypeAnalysisContext)> pairs = [];
        foreach (var type in appContext.Il2CppMscorlib.Types)
        {
            if (type.IsInterface)
            {
                var systemType = appContext.Mscorlib.GetTypeByFullName(type.DefaultFullName);
                if (systemType != null && IsPublic(systemType))
                {
                    pairs.Add((type, systemType));
                }
            }
        }

        return pairs;

        static bool IsPublic(TypeAnalysisContext? type)
        {
            var current = type;
            while (current is not null)
            {
                if (current.Visibility is not TypeAttributes.Public and not TypeAttributes.NestedPublic)
                {
                    return false;
                }
                current = current.DeclaringType;
            }
            return true;
        }
    }

    private static void ImplementInterface(TypeAnalysisContext? il2CppInterface, TypeAnalysisContext injectedInterface, TypeAnalysisContext systemInterface)
    {
        foreach (var systemMethod in systemInterface.Methods)
        {
            if (systemMethod.IsStatic)
            {
                continue;
            }

            var attributes = (systemMethod.Attributes & ~(MethodAttributes.Abstract | MethodAttributes.MemberAccessMask)) | MethodAttributes.Private | MethodAttributes.Final | MethodAttributes.NewSlot;
            var injectedMethod = new InjectedMethodAnalysisContext(injectedInterface, $"{systemInterface.FullName}.{systemMethod.Name}", systemMethod.ReturnType, attributes, [])
            {
                IsInjected = true,
            };
            injectedInterface.Methods.Add(injectedMethod);

            injectedMethod.CopyGenericParameters(systemMethod, true);

            var visitor = TypeReplacementVisitor.CreateForMethodCopying(systemMethod, injectedMethod);

            injectedMethod.SetDefaultReturnType(visitor.Replace(systemMethod.ReturnType));

            foreach (var parameter in systemMethod.Parameters)
            {
                injectedMethod.Parameters.Add(new InjectedParameterAnalysisContext(parameter.Name, visitor.Replace(parameter.ParameterType), parameter.Attributes, parameter.ParameterIndex, injectedMethod));
            }

            injectedMethod.Overrides.Add(systemMethod.MaybeMakeConcreteGeneric(injectedInterface.GenericParameters, injectedMethod.GenericParameters));

            var il2CppMethod = FindIl2CppMethod(il2CppInterface, systemMethod.Name, injectedMethod, out var returnConversion, out var parameterConversions);

            if (il2CppMethod is null)
            {
                injectedMethod.PutExtraData(new NativeMethodBody()
                {
                    Instructions =
                    [
                        new Instruction(CilOpCodes.Ldnull),
                        new Instruction(CilOpCodes.Throw),
                    ]
                });
                continue;
            }

            Debug.Assert(il2CppInterface is not null);

            List<Instruction> instructions = [];
            instructions.Add(CilOpCodes.Ldarg_0);
            instructions.Add(CilOpCodes.Castclass, il2CppInterface.MaybeMakeGenericInstanceType(injectedInterface.GenericParameters));
            for (var i = 0; i < injectedMethod.Parameters.Count; i++)
            {
                var parameter = injectedMethod.Parameters[i];
                instructions.Add(CilOpCodes.Ldarg, parameter);
                parameterConversions[i].Add(instructions);
            }
            instructions.Add(CilOpCodes.Callvirt, il2CppMethod.MaybeMakeConcreteGeneric(injectedInterface.GenericParameters, injectedMethod.GenericParameters));
            returnConversion.Add(instructions);
            instructions.Add(CilOpCodes.Ret);

            injectedMethod.PutExtraData(new NativeMethodBody()
            {
                Instructions = instructions
            });
        }

        // Make the Il2Cpp interface inherit from the injected interface
        if (il2CppInterface is not null)
        {
            Debug.Assert(il2CppInterface.GenericParameters.Count == injectedInterface.GenericParameters.Count);
            il2CppInterface.InterfaceContexts.Add(injectedInterface.MaybeMakeGenericInstanceType(il2CppInterface.GenericParameters));
        }
    }

    private static MethodAnalysisContext? FindIl2CppMethod(TypeAnalysisContext? il2CppInterface, string methodName, MethodAnalysisContext injectedMethod, out Conversion returnConversion, out Conversion[] parameterConversions)
    {
        returnConversion = NullConversion.Instance;
        parameterConversions = new Conversion[injectedMethod.Parameters.Count];

        if (il2CppInterface is null)
        {
            return null;
        }

        // Search in reverse order because the most user-friendly overloads will be last (they always get added to the end of the list).
        // This ensures correct behavior for array parameters, which have special handling in the user-friendly overload processing layer.
        for (var il2CppMethodIndex = il2CppInterface.Methods.Count - 1; il2CppMethodIndex >= 0; il2CppMethodIndex--)
        {
            var il2CppMethod = il2CppInterface.Methods[il2CppMethodIndex];
            if (il2CppMethod.Name == methodName &&
                !il2CppMethod.IsStatic &&
                il2CppMethod.Parameters.Count == injectedMethod.Parameters.Count &&
                il2CppMethod.GenericParameters.Count == injectedMethod.GenericParameters.Count &&
                il2CppMethod.IsVoid == injectedMethod.IsVoid)
            {
                var visitor = TypeReplacementVisitor.CreateForMethodCopying(il2CppMethod, injectedMethod);
                if (!AreTypesEqual(visitor.Replace(il2CppMethod.ReturnType), injectedMethod.ReturnType, out returnConversion))
                {
                    continue;
                }

                var parametersMatch = true;
                for (var i = 0; i < il2CppMethod.Parameters.Count; i++)
                {
                    var injectedParameterType = injectedMethod.Parameters[i].ParameterType;
                    var il2CppParameterType = visitor.Replace(il2CppMethod.Parameters[i].ParameterType);
                    if (!AreTypesEqual(injectedParameterType, il2CppParameterType, out parameterConversions[i]))
                    {
                        parametersMatch = false;
                        break;
                    }
                }
                if (parametersMatch)
                {
                    return il2CppMethod;
                }
            }
        }
        return null;

        static bool AreTypesEqual(TypeAnalysisContext from, TypeAnalysisContext to, out Conversion conversion)
        {
            if (TypeAnalysisContextEqualityComparer.Instance.Equals(from, to))
            {
                conversion = NullConversion.Instance;
                return true;
            }
            else if (from.TryGetConversionTo(to, out var conversionMethod) || to.TryGetConversionFrom(from, out conversionMethod))
            {
                // An implicit or explicit conversion exists
                conversion = new MethodCallConversion(conversionMethod);
                return true;
            }
            else if (to.KnownType is KnownTypeCode.System_Object && !from.IsValueType)
            {
                // Any reference type can be converted to System.Object
                conversion = NullConversion.Instance;
                return true;
            }
            else if (to.KnownType is KnownTypeCode.Il2CppSystem_IObject && from.KnownType is KnownTypeCode.System_Object)
            {
                // obj as IObject
                conversion = new IsInstanceConversion(to);
                return true;
            }
            else if (to.IsInterface && from.IsInterface && to.DefaultFullName == from.DefaultFullName)
            {
                if (to.Namespace.StartsWith("Il2Cpp", StringComparison.Ordinal))
                {
                    // System.IX to Il2CppSystem.IX
                    // Need to cast
                    conversion = new CastClassConversion(to);
                    return true;
                }
                else if (from.Namespace.StartsWith("Il2Cpp", StringComparison.Ordinal))
                {
                    // Il2CppSystem.IX to System.IX
                    // Since the Il2Cpp interface implements the System interface, no conversion is needed.
                    conversion = NullConversion.Instance;
                    return true;
                }
                else
                {
                    Debug.Fail($"Unexpected case where two reference types have the same full name but are not considered equal: {from.FullName} and {to.FullName}");
                    conversion = NullConversion.Instance;
                    return false;
                }
            }
            else
            {
                conversion = NullConversion.Instance;
                return false;
            }
        }
    }
}
