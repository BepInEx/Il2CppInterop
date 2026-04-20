using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Common;
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
        var iil2CppType_get_ObjectClass = iil2CppType.GetMethodByName($"get_{nameof(IIl2CppType.ObjectClass)}");

        var iil2CppTypeGeneric = appContext.ResolveTypeOrThrow(typeof(IIl2CppType<>));
        var iil2CppTypeGeneric_get_Size = iil2CppTypeGeneric.GetMethodByName($"get_{nameof(IIl2CppType<>.Size)}");
        var iil2CppTypeGeneric_get_AssemblyName = iil2CppTypeGeneric.GetMethodByName($"get_{nameof(IIl2CppType<>.AssemblyName)}");
        var iil2CppTypeGeneric_get_Namespace = iil2CppTypeGeneric.GetMethodByName($"get_{nameof(IIl2CppType<>.Namespace)}");
        var iil2CppTypeGeneric_get_Name = iil2CppTypeGeneric.GetMethodByName($"get_{nameof(IIl2CppType<>.Name)}");
        var iil2CppTypeGeneric_ReadFromSpan = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.ReadFromSpan));
        var iil2CppTypeGeneric_WriteToSpan = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.WriteToSpan));

        var il2CppClassPointerStore = appContext.ResolveTypeOrThrow(typeof(Il2CppClassPointerStore<>));
        var il2CppClassPointerStore_NativeClassPointer = il2CppClassPointerStore.GetFieldByName(nameof(Il2CppClassPointerStore<>.NativeClassPointer));

        var il2CppTypeHelper = appContext.ResolveTypeOrThrow(typeof(Il2CppTypeHelper));
        var il2CppTypeHelper_ReadReference = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadReference));
        var il2CppTypeHelper_WriteReference = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteReference));
        var il2CppTypeHelper_ReadFromSpanAtOffset = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadFromSpanAtOffset));
        var il2CppTypeHelper_WriteToSpanAtOffset = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteToSpanAtOffset));
        var il2CppTypeHelper_ReadFromSpanBlittable = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.ReadFromSpanBlittable));
        var il2CppTypeHelper_WriteToSpanBlittable = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteToSpanBlittable));
        var il2CppTypeHelper_WriteToSpan = il2CppTypeHelper.GetMethodByName(nameof(Il2CppTypeHelper.WriteToSpan));

        var intPtr_get_Size = appContext.SystemTypes.SystemIntPtrType.GetMethodByName($"get_{nameof(IntPtr.Size)}");

        var il2CppSystemIObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IObject");
        var il2CppSystemIValueType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IValueType");
        var il2CppSystemIEnum = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IEnum");

        var il2CppSystemValueType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");

        // IValueType.Size
        MethodAnalysisContext il2cppSystemIValueType_get_Size;
        {
            il2cppSystemIValueType_get_Size = new InjectedMethodAnalysisContext(
                il2CppSystemIValueType,
                $"get_{nameof(Il2CppSystem.IValueType.Size)}",
                appContext.SystemTypes.SystemInt32Type,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.SpecialName | MethodAttributes.NewSlot,
                [])
            {
                IsInjected = true,
            };
            il2CppSystemIValueType.Methods.Add(il2cppSystemIValueType_get_Size);

            var property = new InjectedPropertyAnalysisContext(
                nameof(Il2CppSystem.IValueType.Size),
                il2cppSystemIValueType_get_Size.ReturnType,
                il2cppSystemIValueType_get_Size,
                null,
                PropertyAttributes.None,
                il2CppSystemIValueType)
            {
                IsInjected = true,
            };
            il2CppSystemIValueType.Properties.Add(property);
        }

        // IValueType.WriteToSpan(Span<byte>)
        MethodAnalysisContext il2cppSystemIValueType_WriteToSpan;
        {
            var spanOfByteType = il2CppTypeHelper_WriteToSpan.Parameters[^1].ParameterType;
            il2cppSystemIValueType_WriteToSpan = new InjectedMethodAnalysisContext(
                il2CppSystemIValueType,
                nameof(Il2CppSystem.IValueType.WriteToSpan),
                appContext.SystemTypes.SystemVoidType,
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot,
                [spanOfByteType])
            {
                IsInjected = true,
            };
            il2CppSystemIValueType.Methods.Add(il2cppSystemIValueType_WriteToSpan);
        }

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected && type != il2CppSystemIObject && type != il2CppSystemIValueType && type != il2CppSystemIEnum)
                    continue;

                type.InterfaceContexts.Add(iil2CppType);

                var instantiatedType = type.SelfInstantiateIfGeneric();
                var instantiatedIl2CppTypeGeneric = iil2CppTypeGeneric.MakeGenericInstanceType([instantiatedType]);
                type.InterfaceContexts.Add(instantiatedIl2CppTypeGeneric);

                TypeAnalysisContext nameReferenceType;
                TypeAnalysisContext classReferenceType;
                if (type == il2CppSystemIObject)
                {
                    nameReferenceType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
                    classReferenceType = nameReferenceType;
                }
                else if (type == il2CppSystemIValueType)
                {
                    nameReferenceType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");
                    classReferenceType = nameReferenceType;
                }
                else if (type == il2CppSystemIEnum)
                {
                    nameReferenceType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");
                    classReferenceType = nameReferenceType;
                }
                else
                {
                    nameReferenceType = type;
                    classReferenceType = instantiatedType;
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
                    method.Overrides.Add(iil2CppType_get_ObjectClass);
                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldsfld, new ConcreteGenericFieldAnalysisContext(il2CppClassPointerStore_NativeClassPointer, il2CppClassPointerStore.MakeGenericInstanceType([classReferenceType]))),
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
                    {
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
                                instantiatedSizeStorage is not null ? new Instruction(CilOpCodes.Ldsfld, instantiatedSizeStorage) : new Instruction(CilOpCodes.Call, intPtr_get_Size),
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

                    // IValueType.Size
                    if (type.IsValueType || type == il2CppSystemValueType)
                    {
                        var methodName = $"{il2CppSystemIValueType.FullName}.get_{nameof(Il2CppSystem.IValueType.Size)}";
                        var method = new InjectedMethodAnalysisContext(
                            type,
                            methodName,
                            il2cppSystemIValueType_get_Size.ReturnType,
                            MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.SpecialName | MethodAttributes.NewSlot,
                            [])
                        {
                            IsInjected = true,
                        };
                        type.Methods.Add(method);
                        method.Overrides.Add(il2cppSystemIValueType_get_Size);

                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                instantiatedSizeStorage is not null ? new Instruction(CilOpCodes.Ldsfld, instantiatedSizeStorage) : new Instruction(CilOpCodes.Call, intPtr_get_Size),
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });

                        var propertyName = $"{il2CppSystemIValueType.FullName}.{nameof(Il2CppSystem.IValueType.Size)}";
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

                // AssemblyName
                if (assembly.Name != assembly.DefaultName)
                {
                    var instantiated_iil2CppTypeGeneric_get_AssemblyName = new ConcreteGenericMethodAnalysisContext(iil2CppTypeGeneric_get_AssemblyName, [instantiatedType], []);
                    var methodName = $"{instantiatedIl2CppTypeGeneric.FullName}.get_{nameof(IIl2CppType<>.AssemblyName)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        instantiated_iil2CppTypeGeneric_get_AssemblyName.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                        [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(instantiated_iil2CppTypeGeneric_get_AssemblyName);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldstr, assembly.DefaultName),
                            new Instruction(CilOpCodes.Ret),
                        ],
                    });

                    var propertyName = $"{instantiatedIl2CppTypeGeneric.FullName}.{nameof(IIl2CppType<>.AssemblyName)}";
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

                // Namespace
                if (type.Namespace != nameReferenceType.DefaultNamespace)
                {
                    var instantiated_iil2CppTypeGeneric_get_Namespace = new ConcreteGenericMethodAnalysisContext(iil2CppTypeGeneric_get_Namespace, [instantiatedType], []);
                    var methodName = $"{instantiatedIl2CppTypeGeneric.FullName}.get_{nameof(IIl2CppType<>.Namespace)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        instantiated_iil2CppTypeGeneric_get_Namespace.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                        [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(instantiated_iil2CppTypeGeneric_get_Namespace);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldstr, nameReferenceType.DefaultNamespace),
                            new Instruction(CilOpCodes.Ret),
                        ],
                    });

                    var propertyName = $"{instantiatedIl2CppTypeGeneric.FullName}.{nameof(IIl2CppType<>.Namespace)}";
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

                // Name
                if (type.Name != nameReferenceType.DefaultName)
                {
                    var instantiated_iil2CppTypeGeneric_get_Name = new ConcreteGenericMethodAnalysisContext(iil2CppTypeGeneric_get_Name, [instantiatedType], []);
                    var methodName = $"{instantiatedIl2CppTypeGeneric.FullName}.get_{nameof(IIl2CppType<>.Name)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        instantiated_iil2CppTypeGeneric_get_Name.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                        [])
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(instantiated_iil2CppTypeGeneric_get_Name);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldstr, nameReferenceType.DefaultName),
                            new Instruction(CilOpCodes.Ret),
                        ],
                    });

                    var propertyName = $"{instantiatedIl2CppTypeGeneric.FullName}.{nameof(IIl2CppType<>.Name)}";
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
                    method.Overrides.Add(instantiated_iil2CppTypeGeneric_ReadFromSpan);

                    if (!type.IsValueType)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldarg_0),
                                new Instruction(CilOpCodes.Call, il2CppTypeHelper_ReadReference.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(CilOpCodes.Ret),
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

                    if (!type.IsValueType)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldarg_0),
                                new Instruction(CilOpCodes.Ldarg_1),
                                new Instruction(CilOpCodes.Call, il2CppTypeHelper_WriteReference.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(CilOpCodes.Ret),
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
                // IValueType.WriteToSpan
                if (type.IsValueType || type == il2CppSystemValueType)
                {
                    var methodName = $"{il2CppSystemIValueType.FullName}.{nameof(Il2CppSystem.IValueType.WriteToSpan)}";
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        methodName,
                        il2cppSystemIValueType_WriteToSpan.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                        il2cppSystemIValueType_WriteToSpan.Parameters.Select(p => p.ParameterType).ToArray())
                    {
                        IsInjected = true,
                    };
                    type.Methods.Add(method);
                    method.Overrides.Add(il2cppSystemIValueType_WriteToSpan);

                    if (type.IsValueType)
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldarg_0),
                                new Instruction(CilOpCodes.Ldobj, instantiatedType),
                                new Instruction(CilOpCodes.Ldarg_1),
                                new Instruction(CilOpCodes.Call, il2CppTypeHelper_WriteToSpan.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });
                    }
                    else
                    {
                        method.PutExtraData(new NativeMethodBody()
                        {
                            Instructions =
                            [
                                new Instruction(CilOpCodes.Ldarg_0),
                                new Instruction(CilOpCodes.Ldarg_1),
                                new Instruction(CilOpCodes.Call, il2CppTypeHelper_WriteReference.MakeGenericInstanceMethod(instantiatedType)),
                                new Instruction(CilOpCodes.Ret),
                            ],
                        });
                    }
                }
            }
        }
    }
}
