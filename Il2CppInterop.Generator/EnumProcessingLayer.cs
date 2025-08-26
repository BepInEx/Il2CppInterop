using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

public class EnumProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Enum Processor";
    public override string Id => "enum_processor";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var il2CppSystemObject = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemValueType = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");
        var il2CppSystemEnum = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");

        var icomparable = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IComparable");
        var iformattable = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IFormattable");
        var iconvertible = appContext.Il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.IConvertible");

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.DefaultBaseType != il2CppSystemEnum)
                    continue;

                // Sequential layout is required to ensure that this struct has the same memory layout as a C# enum.
                type.OverrideAttributes = (type.Attributes & ~TypeAttributes.LayoutMask) | TypeAttributes.SequentialLayout;

                var valueField = type.Fields.First(f => f.Name == "value__");

                valueField.OverrideAttributes = FieldAttributes.Private | FieldAttributes.InitOnly;

                Debug.Assert(valueField.FieldType == valueField.DefaultFieldType, "Field type should not be overriden.");

                var il2CppType = valueField.FieldType;
                var monoType = appContext.Mscorlib.GetTypeByFullNameOrThrow($"System.{il2CppType.Name}");

                Debug.Assert(monoType != il2CppType);

                type.EnumIl2CppUnderlyingType = il2CppType;
                type.EnumMonoUnderlyingType = monoType;

                // Constructor
                var constructor = new InjectedMethodAnalysisContext(
                    type,
                    ".ctor",
                    appContext.SystemTypes.SystemVoidType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                    [il2CppType],
                    ["value"])
                {
                    IsInjected = true,
                };
                {
                    type.Methods.Add(constructor);

                    var methodBody = new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(OpCodes.Ldarg_0),
                            new Instruction(OpCodes.Ldarg_1),
                            new Instruction(OpCodes.Stfld, valueField),
                            new Instruction(OpCodes.Ret)
                        ]
                    };

                    constructor.PutExtraData(methodBody);
                }

                #region Conversions
                // Conversion: enum -> il2cpp
                var conversionEnumIl2Cpp = new InjectedMethodAnalysisContext(
                    type,
                    "op_Explicit",
                    il2CppType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [type],
                    ["value"])
                {
                    IsInjected = true,
                };
                {
                    type.Methods.Add(conversionEnumIl2Cpp);
                    var methodBody = new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(OpCodes.Ldarga, conversionEnumIl2Cpp.Parameters[0]),
                            new Instruction(OpCodes.Ldfld, valueField),
                            new Instruction(OpCodes.Ret)
                        ]
                    };
                    conversionEnumIl2Cpp.PutExtraData(methodBody);
                }

                // Conversion: il2cpp -> enum
                var conversionIl2CppEnum = new InjectedMethodAnalysisContext(
                    type,
                    "op_Explicit",
                    type,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [il2CppType],
                    ["value"])
                {
                    IsInjected = true,
                };
                {
                    type.Methods.Add(conversionIl2CppEnum);
                    var methodBody = new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(OpCodes.Ldarg_0),
                            new Instruction(OpCodes.Newobj, constructor),
                            new Instruction(OpCodes.Ret)
                        ]
                    };
                    conversionIl2CppEnum.PutExtraData(methodBody);
                }

                // Conversion: enum -> mono
                var conversionEnumMono = new InjectedMethodAnalysisContext(
                    type,
                    "op_Explicit",
                    monoType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [type],
                    ["value"])
                {
                    IsInjected = true,
                };
                {
                    type.Methods.Add(conversionEnumMono);
                    var methodBody = new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(OpCodes.Ldarg_0),
                            new Instruction(OpCodes.Call, conversionEnumIl2Cpp),
                            new Instruction(OpCodes.Call, il2CppType.GetImplicitConversionTo(monoType)),
                            new Instruction(OpCodes.Ret)
                        ]
                    };
                    conversionEnumMono.PutExtraData(methodBody);
                }

                // Conversion: mono -> enum
                var conversionMonoEnum = new InjectedMethodAnalysisContext(
                    type,
                    "op_Explicit",
                    type,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static | MethodAttributes.SpecialName,
                    [monoType],
                    ["value"])
                {
                    IsInjected = true,
                };
                {
                    type.Methods.Add(conversionMonoEnum);
                    var methodBody = new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(OpCodes.Ldarg_0),
                            new Instruction(OpCodes.Call, il2CppType.GetImplicitConversionFrom(monoType)),
                            new Instruction(OpCodes.Call, conversionIl2CppEnum),
                            new Instruction(OpCodes.Ret)
                        ]
                    };
                    conversionMonoEnum.PutExtraData(methodBody);
                }
                #endregion

                #region Implicit Interfaces
                foreach (var @interface in (ReadOnlySpan<TypeAnalysisContext>)[icomparable, iformattable, iconvertible])
                {
                    type.InterfaceContexts.Add(@interface);

                    foreach (var interfaceMethod in @interface.Methods)
                    {
                        if (interfaceMethod.IsInjected || !interfaceMethod.IsVirtual || !interfaceMethod.Attributes.HasFlag(MethodAttributes.NewSlot))
                            continue;

                        var method = new InjectedMethodAnalysisContext(
                            type,
                            $"{@interface.FullName}.{interfaceMethod.Name}",
                            interfaceMethod.ReturnType,
                            interfaceMethod.Attributes & ~MethodAttributes.Abstract | MethodAttributes.Final,
                            interfaceMethod.Parameters.Select(p => p.ParameterType).ToArray(),
                            interfaceMethod.Parameters.Select(p => p.Name).ToArray(),
                            interfaceMethod.Parameters.Select(p => p.Attributes).ToArray())
                        {
                            IsInjected = true,
                            Visibility = MethodAttributes.Private,
                        };
                        method.OverridesList.Add(interfaceMethod);
                        type.Methods.Add(method);

                        var methodBody = new NativeMethodBody()
                        {
                            Instructions = [
                                new Instruction(OpCodes.Ldarg, This.Instance),
                                new Instruction(OpCodes.Ldfld, valueField),
                                new Instruction(OpCodes.Box, valueField.FieldType),
                                .. method.Parameters.Select(p => new Instruction(OpCodes.Ldarg, p)),
                                new Instruction(OpCodes.Callvirt, interfaceMethod),
                                new Instruction(OpCodes.Ret)
                            ]
                        };
                        method.PutExtraData(methodBody);
                    }
                }
                #endregion
            }
        }
    }
}
