using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class PointerConstructorProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Pointer Constructor Processor";
    public override string Id => "pointer_constructor_processor";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var objectPointerType = appContext.ResolveTypeOrThrow(typeof(ObjectPointer));

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected || type.IsInterface)
                    continue;

                Debug.Assert(!type.IsStatic, "Static types should have been marked as instance types by now.");

                var typeInfo = type.GetExtraData<Il2CppTypeInfo>();
                Debug.Assert(typeInfo is not null);

                if (typeInfo.Blittability != TypeBlittability.ReferenceType)
                    continue;

                // Constructors for formerly static types should be private
                var attributes = type.DefaultAttributes.HasFlag(TypeAttributes.Abstract) && type.DefaultAttributes.HasFlag(TypeAttributes.Sealed)
                    ? MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.SpecialName
                    : MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName;

                var constructor = new InjectedMethodAnalysisContext(
                    type,
                    ".ctor",
                    appContext.SystemTypes.SystemVoidType,
                    attributes,
                    [objectPointerType])
                {
                    IsInjected = true,
                };
                type.Methods.Add(constructor);

                type.PointerConstructor = constructor;
            }
        }

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly)
                continue;

            foreach (var type in assembly.Types)
            {
                var constructor = type.PointerConstructor;
                if (constructor is null)
                    continue;

                var baseType = type.BaseType;
                if (baseType is null)
                    continue;

                MethodAnalysisContext? baseConstructor;
                if (baseType is GenericInstanceTypeAnalysisContext genericInstanceType)
                {
                    var m = genericInstanceType.GenericType.PointerConstructor;
                    if (m is null)
                        continue;
                    baseConstructor = new ConcreteGenericMethodAnalysisContext(m, genericInstanceType.GenericArguments, []);
                }
                else
                {
                    baseConstructor = baseType.PointerConstructor;
                    if (baseConstructor is null)
                        continue;
                }

                var methodBody = new NativeMethodBody()
                {
                    Instructions = [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Ldarg_1),
                        new Instruction(OpCodes.Call, baseConstructor),
                        new Instruction(OpCodes.Ret)
                    ]
                };
                constructor.PutExtraData(methodBody);
            }
        }
    }
}
