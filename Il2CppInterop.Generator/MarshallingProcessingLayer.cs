using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class MarshallingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Marshalling Processor";
    public override string Id => "marshalling_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var iil2CppType = appContext.ResolveTypeOrThrow(typeof(IIl2CppType));
        var iil2CppType_get_ObjectClass = iil2CppType.GetMethodByName($"get_{nameof(IIl2CppType.ObjectClass)}");

        var iil2CppTypeGeneric = appContext.ResolveTypeOrThrow(typeof(IIl2CppType<>));
        var iil2CppTypeGeneric_get_Size = iil2CppTypeGeneric.GetMethodByName($"get_{nameof(IIl2CppType<>.Size)}");
        var iil2CppTypeGeneric_ReadFromSpan = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.ReadFromSpan));
        var iil2CppTypeGeneric_WriteToSpan = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.WriteToSpan));

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppType));
        var il2CppTypeHelper_ReadFromSpanAtOffset = il2CppTypeHelper.GetMethodByName(nameof(Il2CppType.ReadFromSpanAtOffset));
        var il2CppTypeHelper_WriteToSpanAtOffset = il2CppTypeHelper.GetMethodByName(nameof(Il2CppType.WriteToSpanAtOffset));
        var il2CppTypeHelper_ReadFromSpanBlittable = il2CppTypeHelper.GetMethodByName(nameof(Il2CppType.ReadFromSpanBlittable));
        var il2CppTypeHelper_WriteToSpanBlittable = il2CppTypeHelper.GetMethodByName(nameof(Il2CppType.WriteToSpanBlittable));
        var il2CppTypeHelper_WriteToSpan = il2CppTypeHelper.GetMethodByName(nameof(Il2CppType.WriteToSpan));
        var il2CppTypeHelper_GetClassPointer = il2CppTypeHelper.Methods.Single(m => m.Name == nameof(Il2CppType.GetClassPointer) && m.GenericParameters.Count == 1);

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected && type.KnownType is not KnownTypeCode.Il2CppSystem_IObject and not KnownTypeCode.Il2CppSystem_IValueType and not KnownTypeCode.Il2CppSystem_IEnum)
                    continue;

                type.InterfaceContexts.Add(iil2CppType);

                var instantiatedType = type.SelfInstantiateIfGeneric();
                var instantiatedIl2CppTypeGeneric = iil2CppTypeGeneric.MakeGenericInstanceType([instantiatedType]);
                type.InterfaceContexts.Add(instantiatedIl2CppTypeGeneric);

                // ObjectClass
                {
                    var methodName = $"{iil2CppType.FullName}.get_{nameof(IIl2CppType.ObjectClass)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        iil2CppType_get_ObjectClass.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName | (type.IsInterface ? MethodAttributes.ReuseSlot : MethodAttributes.NewSlot),
                        [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(iil2CppType_get_ObjectClass);
                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Call, il2CppTypeHelper_GetClassPointer.MakeGenericInstanceMethod(instantiatedType)),
                            new Instruction(CilOpCodes.Ret),
                        ],
                    });

                    var propertyName = $"{iil2CppType.FullName}.{nameof(IIl2CppType.ObjectClass)}";
                    var property = new InjectedPropertyAnalysisContext(
                        propertyName,
                        method.ReturnType,
                        method,
                        null,
                        PropertyAttributes.None,
                        type)
                    {
                        IsInjected = true,
                    };
                    type.Properties.Add(property);
                }

                // Size
                {
                    var instantiatedSizeStorage = type.SizeStorage is null
                        ? null
                        : type.GenericParameters.Count > 0
                            ? type.SizeStorage!.MakeConcreteGeneric(type.GenericParameters)
                            : type.SizeStorage;

                    // IIl2CppType<T>.Size
                    if (instantiatedSizeStorage is null)
                    {
                        // Reference types have a default implementation that returns IntPtr.Size, so we don't need to do anything.
                        Debug.Assert(!type.IsValueType);
                    }
                    else
                    {
                        Debug.Assert(type.IsValueType);
                        var instantiated_iil2CppTypeGeneric_get_Size = new ConcreteGenericMethodAnalysisContext(iil2CppTypeGeneric_get_Size, [instantiatedType], []);
                        var methodName = $"{instantiatedIl2CppTypeGeneric.FullName}.get_{nameof(IIl2CppType<>.Size)}";
                        var method = new InjectedMethodAnalysisContext(
                            type,
                            methodName,
                            instantiated_iil2CppTypeGeneric_get_Size.ReturnType,
                            MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(method);
                        method.Overrides.Add(instantiated_iil2CppTypeGeneric_get_Size);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldsfld, instantiatedSizeStorage),
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });

                        var propertyName = $"{instantiatedIl2CppTypeGeneric.FullName}.{nameof(IIl2CppType<>.Size)}";
                        var property = new InjectedPropertyAnalysisContext(
                            propertyName,
                            method.ReturnType,
                            method,
                            null,
                            PropertyAttributes.None,
                            type)
                        {
                            IsInjected = true,
                        };
                        type.Properties.Add(property);
                    }
                }

                var instanceFieldCount = type.Fields.Count(f => !f.IsStatic);

                // ReadFromSpan
                if (type.IsValueType)
                {
                    var instantiated_iil2CppTypeGeneric_ReadFromSpan = new ConcreteGenericMethodAnalysisContext(iil2CppTypeGeneric_ReadFromSpan, [instantiatedType], []);
                    var methodName = $"{instantiatedIl2CppTypeGeneric.FullName}.{nameof(IIl2CppType<>.ReadFromSpan)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        instantiated_iil2CppTypeGeneric_ReadFromSpan.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static,
                        [instantiated_iil2CppTypeGeneric_ReadFromSpan.Parameters[0].ParameterType])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(instantiated_iil2CppTypeGeneric_ReadFromSpan);

                    if (instanceFieldCount == 0)
                    {
                        LocalVariable local = new(instantiatedType);
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldloca, local),
                                new Instruction(CilOpCodes.Initobj, instantiatedType),
                                new Instruction(CilOpCodes.Ldloc, local),
                                new Instruction(CilOpCodes.Ret),
                            ],
                            LocalVariables = [local],
                        });
                    }
                    else if (type.IsIl2CppPrimitive)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldarg_0),
                                new Instruction(CilOpCodes.Call, il2CppTypeHelper_ReadFromSpanBlittable.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });
                    }
                    else
                    {
                        LocalVariable local = new(instantiatedType);
                        List<Instruction> instructions = new(2 + instanceFieldCount * 5 + 2)
                        {
                            new Instruction(CilOpCodes.Ldloca, local),
                            new Instruction(CilOpCodes.Initobj, instantiatedType)
                        };

                        foreach (var field in type.Fields)
                        {
                            if (field.IsStatic)
                                continue;

                            Debug.Assert(!field.IsInjected);
                            Debug.Assert(field.OffsetStorage is not null);

                            var instantiatedField = field.SelfInstantiateIfNecessary();

                            instructions.Add(new Instruction(CilOpCodes.Ldloca, local));
                            instructions.Add(new Instruction(CilOpCodes.Ldarg_0));
                            instructions.Add(new Instruction(CilOpCodes.Ldsfld, field.OffsetStorage!.MaybeMakeConcreteGeneric(type.GenericParameters)));
                            instructions.Add(new Instruction(CilOpCodes.Call, il2CppTypeHelper_ReadFromSpanAtOffset.MakeGenericInstanceMethod(instantiatedField.FieldType)));
                            instructions.Add(new Instruction(CilOpCodes.Stfld, instantiatedField));
                        }

                        instructions.Add(new Instruction(CilOpCodes.Ldloc, local));
                        instructions.Add(new Instruction(CilOpCodes.Ret));

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [local],
                        });
                    }
                }

                // WriteToSpan
                if (type.IsValueType)
                {
                    var instantiated_iil2CppTypeGeneric_WriteToSpan = new ConcreteGenericMethodAnalysisContext(iil2CppTypeGeneric_WriteToSpan, [instantiatedType], []);
                    var methodName = $"{instantiatedIl2CppTypeGeneric.FullName}.{nameof(IIl2CppType<>.WriteToSpan)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        instantiated_iil2CppTypeGeneric_WriteToSpan.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static,
                        instantiated_iil2CppTypeGeneric_WriteToSpan.Parameters.Select(p => p.ParameterType).ToArray())
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(instantiated_iil2CppTypeGeneric_WriteToSpan);

                    if (instanceFieldCount == 0)
                    {
                        // Struct with no instance fields - nothing to do.
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });
                    }
                    else if (type.IsIl2CppPrimitive)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldarg_0),
                                new Instruction(CilOpCodes.Ldarg_1),
                                new Instruction(CilOpCodes.Call, il2CppTypeHelper_WriteToSpanBlittable.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });
                    }
                    else
                    {
                        List<Instruction> instructions = new(instanceFieldCount * 5 + 1);
                        foreach (var field in type.Fields)
                        {
                            if (field.IsStatic)
                                continue;

                            Debug.Assert(!field.IsInjected);
                            Debug.Assert(field.OffsetStorage is not null);

                            var instantiatedField = field.SelfInstantiateIfNecessary();

                            instructions.Add(new Instruction(CilOpCodes.Ldarga, method.Parameters[0]));
                            instructions.Add(new Instruction(CilOpCodes.Ldfld, instantiatedField));
                            instructions.Add(new Instruction(CilOpCodes.Ldarg_1));
                            instructions.Add(new Instruction(CilOpCodes.Ldsfld, field.OffsetStorage!.MaybeMakeConcreteGeneric(type.GenericParameters)));
                            instructions.Add(new Instruction(CilOpCodes.Call, il2CppTypeHelper_WriteToSpanAtOffset.MakeGenericInstanceMethod(instantiatedField.FieldType)));
                        }
                        instructions.Add(new Instruction(CilOpCodes.Ret));

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                        });
                    }
                }
            }
        }
    }
}
