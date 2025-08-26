using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator;

public class ExceptionHierarchyProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Exception Hierarchy";
    public override string Id => "exception_hierarchy";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var systemExceptionType = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.Exception");

        var il2CppSystemExceptionType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Exception");

        var il2CppExceptionInterface = appContext.ResolveTypeOrThrow(typeof(IIl2CppException));
        var il2CppExceptionInterfaceMethod = il2CppExceptionInterface.GetMethodByName(nameof(IIl2CppException.CreateSystemException));
        var il2CppExceptionInterfaceMethodName = $"{il2CppExceptionInterface.FullName}.{il2CppExceptionInterfaceMethod.Name}";

        var exceptionTypes = GetExceptionTypes(appContext);

        foreach (var exceptionType in exceptionTypes)
        {
            var nestedExceptionType = exceptionType.InjectNestedType(GetUniqueNameForNestedClass(exceptionType), null, TypeAttributes.NestedPublic);
            nestedExceptionType.IsInjected = true;
            exceptionType.SystemExceptionType = nestedExceptionType;

            // Copy generic parameters
            if (exceptionType.HasGenericParameters)
            {
                foreach (var genericParameter in exceptionType.GenericParameters)
                {
                    nestedExceptionType.GenericParameters.Add(new GenericParameterTypeAnalysisContext(
                        genericParameter.Name,
                        genericParameter.Index,
                        genericParameter.Type,
                        genericParameter.Attributes & GenericParameterAttributes.AllowByRefLike,
                        nestedExceptionType));
                }
            }

            // Inject constructor
            nestedExceptionType.InjectMethodContext(
                ".ctor",
                appContext.SystemTypes.SystemVoidType,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                il2CppSystemExceptionType);
        }

        foreach (var exceptionType in exceptionTypes)
        {
            var nestedExceptionType = exceptionType.SystemExceptionType!;

            // Base type
            Debug.Assert(exceptionType.BaseType is not null);
            if (exceptionType == il2CppSystemExceptionType)
            {
                nestedExceptionType.SetDefaultBaseType(appContext.ResolveTypeOrThrow(typeof(Il2CppException)));
            }
            else if (exceptionType.BaseType is GenericInstanceTypeAnalysisContext exceptionBaseTypeGenericInstance)
            {
                // Build replacements dictionary
                Dictionary<TypeAnalysisContext, TypeAnalysisContext> replacements = new();
                for (var i = 0; i < exceptionType.GenericParameters.Count; i++)
                {
                    replacements.Add(exceptionType.GenericParameters[i], nestedExceptionType.GenericParameters[i]);
                }
                replacements.Add(exceptionBaseTypeGenericInstance.GenericType, exceptionBaseTypeGenericInstance.GenericType.SystemExceptionType!);

                nestedExceptionType.SetDefaultBaseType(new TypeReplacementVisitor(replacements).Replace(exceptionBaseTypeGenericInstance));
            }
            else
            {
                nestedExceptionType.SetDefaultBaseType(exceptionType.BaseType.SystemExceptionType);
            }

            // Constructor
            Debug.Assert(nestedExceptionType.Methods.Count is 1);
            var constructor = nestedExceptionType.Methods[0];
            Debug.Assert(nestedExceptionType.BaseType is not null);
            MethodAnalysisContext baseConstructor;
            if (nestedExceptionType.BaseType is GenericInstanceTypeAnalysisContext nestedExceptionBaseTypeGenericInstance)
            {
                baseConstructor = new ConcreteGenericMethodAnalysisContext(
                    nestedExceptionBaseTypeGenericInstance.GenericType.GetMethodByName(".ctor"),
                    nestedExceptionBaseTypeGenericInstance.GenericArguments,
                    []);
            }
            else
            {
                baseConstructor = nestedExceptionType.BaseType.GetMethodByName(".ctor");
            }
            constructor.PutExtraData(new NativeMethodBody()
            {
                Instructions =
                [
                    new(OpCodes.Ldarg_0), // this
                    new(OpCodes.Ldarg_1), // object
                    new(OpCodes.Call, baseConstructor),
                    new(OpCodes.Ret),
                ],
            });

            // Interface implementation
            exceptionType.InterfaceContexts.Add(il2CppExceptionInterface);
            {
                var interfaceMethod = new InjectedMethodAnalysisContext(
                    exceptionType,
                    il2CppExceptionInterfaceMethodName,
                    systemExceptionType,
                    MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                    [])
                {
                    IsInjected = true
                };
                exceptionType.Methods.Add(interfaceMethod);
                interfaceMethod.PutExtraData(new NativeMethodBody()
                {
                    Instructions =
                    [
                        new(OpCodes.Ldarg_0), // this
                        new(OpCodes.Newobj, exceptionType.HasGenericParameters ? new ConcreteGenericMethodAnalysisContext(constructor, exceptionType.GenericParameters, []) : constructor),
                        new(OpCodes.Ret),
                    ],
                });
                interfaceMethod.OverridesList.Add(il2CppExceptionInterfaceMethod);
            }
        }
    }

    private static HashSet<TypeAnalysisContext> GetExceptionTypes(ApplicationAnalysisContext appContext)
    {
        var exceptionTypes = new HashSet<TypeAnalysisContext>();
        var nonExceptionTypes = new HashSet<TypeAnalysisContext>();

        // Add Il2CppSystem.Exception
        {
            exceptionTypes.Add(appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Exception"));
        }

        // Add all derived types of Il2CppSystem.Exception
        foreach (var type in appContext.AllTypes)
        {
            AddTypeToSet(type, exceptionTypes, nonExceptionTypes);
        }

        return exceptionTypes;

        static bool AddTypeToSet(TypeAnalysisContext? type, HashSet<TypeAnalysisContext> exceptionTypes, HashSet<TypeAnalysisContext> nonExceptionTypes)
        {
            if (type is GenericInstanceTypeAnalysisContext genericInstance)
            {
                return AddTypeToSet(genericInstance.GenericType, exceptionTypes, nonExceptionTypes);
            }
            Debug.Assert(type is not ReferencedTypeAnalysisContext);
            if (type == null)
            {
                return false;
            }
            if (exceptionTypes.Contains(type))
            {
                return true;
            }
            if (nonExceptionTypes.Contains(type))
            {
                return false;
            }
            var isException = AddTypeToSet(type.BaseType, exceptionTypes, nonExceptionTypes);
            if (isException)
            {
                exceptionTypes.Add(type);
            }
            else
            {
                nonExceptionTypes.Add(type);
            }
            return isException;
        }
    }

    private static string GetUniqueNameForNestedClass(TypeAnalysisContext declaringType)
    {
        var genericParameterCount = declaringType.GenericParameters.Count;
        var nonGenericName = "Exception";
        while (true)
        {
            var name = genericParameterCount > 0 ? $"{nonGenericName}`{genericParameterCount}" : nonGenericName;
            if (declaringType.Name == name || declaringType.NestedTypes.Any(t => t.Name == name))
            {
                nonGenericName += "_";
            }
            else
            {
                return name;
            }
        }
    }
}
