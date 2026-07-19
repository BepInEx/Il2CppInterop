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

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.DefaultBaseType != il2CppSystemEnum)
                    continue;

                // Sequential layout is required to ensure that this struct has the same memory layout as a C# enum.
                type.OverrideAttributes = (type.Attributes & ~TypeAttributes.LayoutMask) | TypeAttributes.SequentialLayout;

                var valueField = type.Fields.First(f => f.Name == "value__");

                valueField.OverrideAttributes = FieldAttributes.Private;

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
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Ldarg_1),
                            new Instruction(CilOpCodes.Stfld, valueField),
                            new Instruction(CilOpCodes.Ret)
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
                            new Instruction(CilOpCodes.Ldarga, conversionEnumIl2Cpp.Parameters[0]),
                            new Instruction(CilOpCodes.Ldfld, valueField),
                            new Instruction(CilOpCodes.Ret)
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
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Newobj, constructor),
                            new Instruction(CilOpCodes.Ret)
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
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, conversionEnumIl2Cpp),
                            new Instruction(CilOpCodes.Call, il2CppType.GetImplicitConversionTo(monoType)),
                            new Instruction(CilOpCodes.Ret)
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
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, il2CppType.GetImplicitConversionFrom(monoType)),
                            new Instruction(CilOpCodes.Call, conversionIl2CppEnum),
                            new Instruction(CilOpCodes.Ret)
                        ]
                    };
                    conversionMonoEnum.PutExtraData(methodBody);
                }
                #endregion

                #region Bitwise Operators
                // &
                {
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        "op_BitwiseAnd",
                        type,
                        MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                        [type, type])
                    {
                        IsInjected = true,
                    };

                    type.Methods.Add(method);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.Ldarg_1),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.And),
                            new Instruction(CilOpCodes.Call, conversionMonoEnum),
                            new Instruction(CilOpCodes.Ret)
                        ]
                    });
                }

                // |
                {
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        "op_BitwiseOr",
                        type,
                        MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                        [type, type])
                    {
                        IsInjected = true,
                    };

                    type.Methods.Add(method);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.Ldarg_1),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.Or),
                            new Instruction(CilOpCodes.Call, conversionMonoEnum),
                            new Instruction(CilOpCodes.Ret)
                        ]
                    });
                }

                // ^
                {
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        "op_ExclusiveOr",
                        type,
                        MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                        [type, type])
                    {
                        IsInjected = true,
                    };

                    type.Methods.Add(method);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.Ldarg_1),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.Xor),
                            new Instruction(CilOpCodes.Call, conversionMonoEnum),
                            new Instruction(CilOpCodes.Ret)
                        ]
                    });
                }

                // ~
                {
                    var method = new InjectedMethodAnalysisContext(
                        type,
                        "op_OnesComplement",
                        type,
                        MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig | MethodAttributes.SpecialName,
                        [type])
                    {
                        IsInjected = true,
                    };

                    type.Methods.Add(method);

                    method.PutExtraData(new NativeMethodBody()
                    {
                        Instructions = [
                            new Instruction(CilOpCodes.Ldarg_0),
                            new Instruction(CilOpCodes.Call, conversionEnumMono),
                            new Instruction(CilOpCodes.Not),
                            new Instruction(CilOpCodes.Call, conversionMonoEnum),
                            new Instruction(CilOpCodes.Ret)
                        ]
                    });
                }
                #endregion
            }
        }
    }
}
