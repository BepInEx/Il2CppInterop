using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class MethodInvokerProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Method Invoker Processor";
    public override string Id => "method_invoker_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var voidPointerType = appContext.SystemTypes.SystemVoidType.MakePointerType();

        var byReference = appContext.ResolveTypeOrThrow(typeof(ByReference<>));
        var byReference_GetValue = byReference.GetMethodByName(nameof(ByReference<>.GetValue));
        var byReference_CopyFrom = byReference.GetMethodByName(nameof(ByReference<>.CopyFrom));
        var byReference_CopyTo = byReference.GetMethodByName(nameof(ByReference<>.CopyTo));
        var byReference_Clear = byReference.GetMethodByName(nameof(ByReference<>.Clear));
        var byReference_ToPointer = byReference.GetMethodByName(nameof(ByReference<>.ToPointer));
        var byReference_Constructor = byReference.GetMethodByName(".ctor");

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper));
        var il2CppTypeHelper_SizeOf = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.SizeOf));

        var il2CppStaticClass = appContext.ResolveTypeOrThrow(typeof(IL2CPP));
        var newObjectPointer = il2CppStaticClass.GetMethodByName(nameof(IL2CPP.NewObjectPointer));

        var iil2CppObjectBase = appContext.ResolveTypeOrThrow(typeof(IIl2CppObjectBase));
        var iil2CppObjectBase_get_Pointer = iil2CppObjectBase.GetMethodByName($"get_{nameof(IIl2CppObjectBase.Pointer)}");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                var instantiatedType = type.SelfInstantiateIfGeneric();

                HashSet<string> existingNames = [type.CSharpName, .. type.Members.Select(m => m.Name)];

                for (var methodIndex = 0; methodIndex < type.Methods.Count; methodIndex++)
                {
                    var method = type.Methods[methodIndex];
                    if (method.IsInjected)
                        continue;

                    InjectedMethodAnalysisContext invoker;
                    {
                        var name = GetNonConflictingName(method.IsInstanceConstructor ? "UnsafeConstruct" : $"UnsafeInvoke_{method.Name}", existingNames);

                        invoker = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            type, // Placeholder return type
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(invoker);

                        method.UnsafeInvokeMethod = invoker;

                        foreach (var gp in method.GenericParameters)
                        {
                            invoker.GenericParameters.Add(new GenericParameterTypeAnalysisContext(gp.Name, gp.Index, gp.Type, gp.Attributes, invoker));
                        }

                        TypeReplacementVisitor visitor = new(Enumerable.Range(0, method.GenericParameters.Count).ToDictionary<int, TypeAnalysisContext, TypeAnalysisContext>(i => method.GenericParameters[i], i => invoker.GenericParameters[i]));

                        for (var i = 0; i < method.GenericParameters.Count; i++)
                        {
                            var originalGp = method.GenericParameters[i];
                            var newGp = invoker.GenericParameters[i];
                            foreach (var constraint in originalGp.ConstraintTypes)
                            {
                                newGp.ConstraintTypes.Add(visitor.Replace(constraint));
                            }
                        }

                        invoker.SetDefaultReturnType(visitor.Replace(method.ReturnType));

                        if (!method.IsStatic)
                        {
                            var newParameter = new InjectedParameterAnalysisContext(
                                null,
                                voidPointerType,
                                ParameterAttributes.None,
                                0,
                                invoker);
                            invoker.Parameters.Add(newParameter);
                        }

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = byReference.MakeGenericInstanceType([visitor.Replace(originalParameter.ParameterType)]);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                invoker.Parameters.Count,
                                invoker);
                            invoker.Parameters.Add(newParameter);
                        }
                    }

                    if (method.IsInstanceConstructor)
                    {
                        var name = GetNonConflictingName("UnsafeCreate", existingNames);

                        var creator = new InjectedMethodAnalysisContext(
                            type,
                            name,
                            instantiatedType,
                            MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(creator);

                        method.UnsafeCreateMethod = creator;

                        Debug.Assert(method.GenericParameters.Count == 0, "Constructors can't have generic parameters.");

                        foreach (var originalParameter in method.Parameters)
                        {
                            var newParameterType = byReference.MakeGenericInstanceType([originalParameter.ParameterType]);

                            var newParameter = new InjectedParameterAnalysisContext(
                                originalParameter.Name,
                                newParameterType,
                                originalParameter.Attributes,
                                creator.Parameters.Count,
                                creator);
                            creator.Parameters.Add(newParameter);
                        }

                        List<Instruction> instructions = new();

                        LocalVariable local;
                        if (type.IsValueType)
                        {
                            local = new(byReference.MakeGenericInstanceType([instantiatedType]));

                            instructions.Add(new Instruction(OpCodes.Call, il2CppTypeHelper_SizeOf.MakeGenericInstanceMethod(instantiatedType)));
                            instructions.Add(new Instruction(OpCodes.Conv_U));
                            instructions.Add(new Instruction(OpCodes.Localloc));
                            instructions.Add(new Instruction(OpCodes.Newobj, new ConcreteGenericMethodAnalysisContext(byReference_Constructor, [instantiatedType], [])));
                            instructions.Add(new Instruction(OpCodes.Stloc, local));

                            instructions.Add(OpCodes.Ldloca, local);
                            instructions.Add(OpCodes.Call, byReference_Clear.MakeConcreteGeneric([instantiatedType], []));

                            instructions.Add(OpCodes.Ldloca, local);
                            instructions.Add(OpCodes.Call, byReference_ToPointer.MakeConcreteGeneric([instantiatedType], []));
                        }
                        else
                        {
                            local = new(instantiatedType);

                            Debug.Assert(type.PointerConstructor is not null);

                            instructions.Add(OpCodes.Call, newObjectPointer.MakeGenericInstanceMethod(instantiatedType));
                            instructions.Add(OpCodes.Newobj, type.PointerConstructor!.MaybeMakeConcreteGeneric(type.GenericParameters, []));
                            instructions.Add(OpCodes.Stloc, local);

                            instructions.Add(OpCodes.Ldloc, local);
                            instructions.Add(OpCodes.Call, iil2CppObjectBase_get_Pointer);
                            instructions.Add(OpCodes.Conv_U);
                        }

                        foreach (var parameter in creator.Parameters)
                        {
                            instructions.Add(new Instruction(OpCodes.Ldarg, parameter));
                        }

                        instructions.Add(OpCodes.Call, invoker.MaybeMakeConcreteGeneric(type.GenericParameters, []));

                        if (type.IsValueType)
                        {
                            instructions.Add(OpCodes.Ldloca, local);
                            instructions.Add(OpCodes.Call, byReference_GetValue.MakeConcreteGeneric([instantiatedType], []));
                        }
                        else
                        {
                            instructions.Add(OpCodes.Ldloc, local);
                        }
                        instructions.Add(OpCodes.Ret);

                        creator.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [local],
                        });
                    }
                }
            }
        }
    }

    private static string GetNonConflictingName(string baseName, HashSet<string> existingNames)
    {
        var name = baseName;
        while (existingNames.Contains(name))
        {
            name = $"{baseName}_";
        }
        return name;
    }
}
