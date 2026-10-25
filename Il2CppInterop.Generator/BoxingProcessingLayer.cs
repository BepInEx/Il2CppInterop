using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator;

public sealed class BoxingProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Boxing Implementations";
    public override string Id => "boxing_implementations";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var iil2CppType = appContext.ResolveTypeOrThrow(typeof(IIl2CppType));
        var iil2CppType_BoxNative = iil2CppType.GetMethodByName(nameof(IIl2CppType.BoxNative));
        var iil2CppType_Box = iil2CppType.GetMethodByName(nameof(IIl2CppType.Box));

        var iil2CppTypeGeneric = appContext.ResolveTypeOrThrow(typeof(IIl2CppType<>));
        var iil2CppTypeGeneric_UnboxNative = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.UnboxNative));
        var iil2CppTypeGeneric_Unbox = iil2CppTypeGeneric.GetMethodByName(nameof(IIl2CppType<>.Unbox));

        var nativeBoxing = appContext.ResolveTypeOrThrow(typeof(NativeBoxing));
        var nativeBoxing_BoxReferenceType = nativeBoxing.GetMethodByName(nameof(NativeBoxing.BoxReferenceType));
        var nativeBoxing_BoxValueType = nativeBoxing.GetMethodByName(nameof(NativeBoxing.BoxValueType));
        var nativeBoxing_BoxNullableValueType = nativeBoxing.GetMethodByName(nameof(NativeBoxing.BoxNullableValueType));
        var nativeBoxing_UnboxNullableValueType = nativeBoxing.GetMethodByName(nameof(NativeBoxing.UnboxNullableValueType));

        var generationInternals = appContext.ResolveTypeOrThrow(typeof(GenerationInternals));
        var generationInternals_BoxNullableValueType = generationInternals.GetMethodByName(nameof(GenerationInternals.BoxNullableValueType));
        var generationInternals_UnboxNullableValueType = generationInternals.GetMethodByName(nameof(GenerationInternals.UnboxNullableValueType));

        var objectPointer = appContext.ResolveTypeOrThrow(typeof(ObjectPointer));

        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemNullable = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Nullable`1");

        // Il2CppSystem.Object needs to implement BoxNative
        {
            var method = new InjectedMethodAnalysisContext(
                il2CppSystemObject,
                $"{iil2CppType.FullName}.{iil2CppType_BoxNative.Name}",
                iil2CppType_BoxNative.ReturnType,
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                [])
            {
                IsInjected = true,
            };
            method.Overrides.Add(iil2CppType_BoxNative);
            il2CppSystemObject.Methods.Add(method);
            method.PutExtraData(new NativeMethodBody()
            {
                Instructions =
                [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Call, nativeBoxing_BoxReferenceType),
                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        // Il2CppSystem.Nullable<T> needs to implement UnboxNative
        {
            var interfaceMethod = iil2CppTypeGeneric_UnboxNative.MakeConcreteGeneric([il2CppSystemNullable.SelfInstantiateIfGeneric()], []);
            var method = new InjectedMethodAnalysisContext(
                il2CppSystemNullable,
                $"{interfaceMethod.DeclaringType!.FullName}.{interfaceMethod.Name}",
                interfaceMethod.ReturnType,
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static,
                [interfaceMethod.Parameters[0].ParameterType])
            {
                IsInjected = true,
            };
            method.Overrides.Add(interfaceMethod);
            il2CppSystemNullable.Methods.Add(method);
            method.PutExtraData(new NativeMethodBody()
            {
                Instructions =
                [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Call, nativeBoxing_UnboxNullableValueType.MakeConcreteGeneric([], il2CppSystemNullable.GenericParameters)),
                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        // Il2CppSystem.Nullable<T> needs to implement Box
        {
            var method = new InjectedMethodAnalysisContext(
                il2CppSystemNullable,
                $"{iil2CppType.FullName}.{iil2CppType_Box.Name}",
                iil2CppType_Box.ReturnType,
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                [])
            {
                IsInjected = true,
            };
            method.Overrides.Add(iil2CppType_Box);
            il2CppSystemNullable.Methods.Add(method);
            method.PutExtraData(new NativeMethodBody()
            {
                Instructions =
                [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Call, generationInternals_BoxNullableValueType.MakeGenericInstanceMethod(il2CppSystemNullable.GenericParameters)),
                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        // Il2CppSystem.Nullable<T> needs to implement Unbox
        {
            var interfaceMethod = iil2CppTypeGeneric_Unbox.MakeConcreteGeneric([il2CppSystemNullable.SelfInstantiateIfGeneric()], []);
            var method = new InjectedMethodAnalysisContext(
                il2CppSystemNullable,
                $"{interfaceMethod.DeclaringType!.FullName}.{interfaceMethod.Name}",
                interfaceMethod.ReturnType,
                MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static,
                [interfaceMethod.Parameters[0].ParameterType])
            {
                IsInjected = true,
            };
            method.Overrides.Add(interfaceMethod);
            il2CppSystemNullable.Methods.Add(method);
            method.PutExtraData(new NativeMethodBody()
            {
                Instructions =
                [
                    new Instruction(CilOpCodes.Ldarg_0),
                    new Instruction(CilOpCodes.Call, generationInternals_UnboxNullableValueType.MakeGenericInstanceMethod(il2CppSystemNullable.GenericParameters)),
                    new Instruction(CilOpCodes.Ret),
                ]
            });
        }

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                if (type.IsValueType)
                {
                    // Value types need to implement BoxNative
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        $"{iil2CppType.FullName}.{iil2CppType_BoxNative.Name}",
                        iil2CppType_BoxNative.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.Final | MethodAttributes.NewSlot,
                        [])
                    {
                        IsInjected = true,
                    };
                    method.Overrides.Add(iil2CppType_BoxNative);
                    type.Methods.Add(method);

                    var boxMethod = type == il2CppSystemNullable
                        ? nativeBoxing_BoxNullableValueType.MakeConcreteGeneric([], type.GenericParameters)
                        : nativeBoxing_BoxValueType.MaybeMakeConcreteGeneric([], [type.SelfInstantiateIfGeneric()]);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, boxMethod),
                            new Instruction(CilOpCodes.Ret),
                        ]
                    });
                }
                else if (!type.IsAbstract && !type.IsInterface)
                {
                    // Concrete reference types need to implement UnboxNative
                    var interfaceMethod = iil2CppTypeGeneric_UnboxNative.MakeConcreteGeneric([type.SelfInstantiateIfGeneric()], []);
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        $"{interfaceMethod.DeclaringType!.FullName}.{interfaceMethod.Name}",
                        interfaceMethod.ReturnType,
                        MethodAttributes.Private | MethodAttributes.HideBySig | MethodAttributes.Static,
                        [interfaceMethod.Parameters[0].ParameterType])
                    {
                        IsInjected = true,
                    };
                    method.Overrides.Add(interfaceMethod);
                    type.Methods.Add(method);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions =
                        [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Newobj, GetObjectPointerConstructor(type, objectPointer)),
                            new Instruction(CilOpCodes.Ret),
                        ]
                    });
                }
            }
        }
    }

    private static MethodAnalysisContext GetObjectPointerConstructor(TypeAnalysisContext declaringType, TypeAnalysisContext objectPointer)
    {
        MethodAnalysisContext? constructor = null;
        foreach (var method in declaringType.Methods)
        {
            if (method.IsInstanceConstructor && method.Parameters.Count is 1 && method.Parameters[0].ParameterType == objectPointer)
            {
                constructor = method;
                break;
            }
        }
        if (constructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found on {declaringType.FullName} that takes an ObjectPointer");
        }
        return constructor.MaybeMakeConcreteGeneric(declaringType.GenericParameters, []);
    }
}
