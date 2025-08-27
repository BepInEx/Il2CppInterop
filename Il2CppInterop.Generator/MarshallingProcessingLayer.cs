using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class MarshallingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Marshalling Processor";
    public override string Id => "marshalling_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var iil2CppType = appContext.ResolveTypeOrThrow(typeof(IIl2CppType));
        var iil2CppType_get_Size = iil2CppType.GetMethodByName($"get_{nameof(IIl2CppType.Size)}");
        var iil2CppType_get_ObjectClass = iil2CppType.GetMethodByName($"get_{nameof(IIl2CppType.ObjectClass)}");

        var iil2CppTypeGeneric = appContext.ResolveTypeOrThrow(typeof(IIl2CppType<>));
        var iil2CppTypeGeneric_ReadFromSpan = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.ReadFromSpan));
        var iil2CppTypeGeneric_WriteToSpan = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.WriteToSpan));

        var il2CppClassPointerStore = appContext.ResolveTypeOrThrow(typeof(Il2CppClassPointerStore<>));
        var il2CppClassPointerStore_NativeClassPtr = il2CppClassPointerStore.GetFieldByName(nameof(Il2CppClassPointerStore<>.NativeClassPtr));

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper));
        var il2CppTypeHelper_ReadReference = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadReference));
        var il2CppTypeHelper_WriteReference = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteReference));
        var il2CppTypeHelper_ReadFromSpanAtOffset = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadFromSpanAtOffset));
        var il2CppTypeHelper_WriteToSpanAtOffset = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteToSpanAtOffset));
        var il2CppTypeHelper_ReadFromSpanBlittable = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadFromSpanBlittable));
        var il2CppTypeHelper_WriteToSpanBlittable = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteToSpanBlittable));

        var intPtr_get_Size = appContext.SystemTypes.SystemIntPtrType.GetMethodByName($"get_{nameof(IntPtr.Size)}");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                type.InterfaceContexts.Add(iil2CppType);

                var instantiatedType = type.SelfInstantiateIfGeneric();
                var instantiatedIl2CppTypeGeneric = iil2CppTypeGeneric.MakeGenericInstanceType([instantiatedType]);
                type.InterfaceContexts.Add(instantiatedIl2CppTypeGeneric);

                // Size
                {
                    var methodName = $"{iil2CppType.FullName}.get_{nameof(IIl2CppType.Size)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        iil2CppType_get_Size.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                        [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.OverridesList.Add(iil2CppType_get_Size);

                    var instantiatedSizeStorage = type.SizeStorage is null
                        ? null
                        : type.GenericParameters.Count > 0
                            ? type.SizeStorage!.MakeConcreteGeneric(type.GenericParameters)
                            : type.SizeStorage;

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            instantiatedSizeStorage is not null ? new Instruction(OpCodes.Ldsfld, instantiatedSizeStorage) : new Instruction(OpCodes.Call, intPtr_get_Size),
                            new Instruction(OpCodes.Ret),
                        ],
                    });

                    var propertyName = $"{type.FullName}.{nameof(IIl2CppType.Size)}";
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
                    method.OverridesList.Add(iil2CppType_get_ObjectClass);
                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(OpCodes.Ldsfld, new ConcreteGenericFieldAnalysisContext(il2CppClassPointerStore_NativeClassPtr, il2CppClassPointerStore.MakeGenericInstanceType([instantiatedType]))),
                            new Instruction(OpCodes.Ret),
                        ],
                    });

                    var propertyName = $"{type.FullName}.{nameof(IIl2CppType.ObjectClass)}";
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

                var instanceFieldCount = type.Fields.Count(f => !f.IsStatic);

                // ReadFromSpan
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
                    method.OverridesList.Add(instantiated_iil2CppTypeGeneric_ReadFromSpan);

                    if (!type.IsValueType)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(OpCodes.Ldarg_0),
                                new Instruction(OpCodes.Call, il2CppTypeHelper_ReadReference.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(OpCodes.Ret),
                            ],
                        });
                    }
                    else if (instanceFieldCount == 0)
                    {
                        LocalVariable local = new(instantiatedType);
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(OpCodes.Ldloca, local),
                                new Instruction(OpCodes.Initobj, instantiatedType),
                                new Instruction(OpCodes.Ldloc, local),
                                new Instruction(OpCodes.Ret),
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
                                new Instruction(OpCodes.Ldarg_0),
                                new Instruction(OpCodes.Call, il2CppTypeHelper_ReadFromSpanBlittable.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(OpCodes.Ret),
                            ],
                        });
                    }
                    else
                    {
                        LocalVariable local = new(instantiatedType);
                        List<Instruction> instructions = new(2 + instanceFieldCount * 5 + 2)
                        {
                            new Instruction(OpCodes.Ldloca, local),
                            new Instruction(OpCodes.Initobj, instantiatedType)
                        };

                        foreach (var field in type.Fields)
                        {
                            if (field.IsStatic)
                                continue;

                            Debug.Assert(!field.IsInjected);
                            Debug.Assert(field.OffsetStorage is not null);

                            var instantiatedField = field.SelfInstantiateIfNecessary();

                            instructions.Add(new Instruction(OpCodes.Ldloca, local));
                            instructions.Add(new Instruction(OpCodes.Ldarg_0));
                            instructions.Add(new Instruction(OpCodes.Ldsfld, field.OffsetStorage!.MaybeMakeConcreteGeneric(type.GenericParameters)));
                            instructions.Add(new Instruction(OpCodes.Call, il2CppTypeHelper_ReadFromSpanAtOffset.MakeGenericInstanceMethod(instantiatedField.FieldType)));
                            instructions.Add(new Instruction(OpCodes.Stfld, instantiatedField));
                        }

                        instructions.Add(new Instruction(OpCodes.Ldloc, local));
                        instructions.Add(new Instruction(OpCodes.Ret));

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions = instructions,
                            LocalVariables = [local],
                        });
                    }
                }

                // WriteToSpan
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
                    method.OverridesList.Add(instantiated_iil2CppTypeGeneric_WriteToSpan);

                    if (!type.IsValueType)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(OpCodes.Ldarg_0),
                                new Instruction(OpCodes.Ldarg_1),
                                new Instruction(OpCodes.Call, il2CppTypeHelper_WriteReference.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(OpCodes.Ret),
                            ],
                        });
                    }
                    else if (instanceFieldCount == 0)
                    {
                        // Struct with no instance fields - nothing to do.
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(OpCodes.Ret),
                            ],
                        });
                    }
                    else if (type.IsIl2CppPrimitive)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(OpCodes.Ldarg_0),
                                new Instruction(OpCodes.Ldarg_1),
                                new Instruction(OpCodes.Call, il2CppTypeHelper_WriteToSpanBlittable.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(OpCodes.Ret),
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

                            instructions.Add(new Instruction(OpCodes.Ldarga, method.Parameters[0]));
                            instructions.Add(new Instruction(OpCodes.Ldfld, instantiatedField));
                            instructions.Add(new Instruction(OpCodes.Ldarg_1));
                            instructions.Add(new Instruction(OpCodes.Ldsfld, field.OffsetStorage!.MaybeMakeConcreteGeneric(type.GenericParameters)));
                            instructions.Add(new Instruction(OpCodes.Call, il2CppTypeHelper_WriteToSpanAtOffset.MakeGenericInstanceMethod(instantiatedField.FieldType)));
                        }
                        instructions.Add(new Instruction(OpCodes.Ret));

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
