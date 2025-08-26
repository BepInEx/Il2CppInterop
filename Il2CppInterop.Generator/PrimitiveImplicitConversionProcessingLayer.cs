using System.Diagnostics;
using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Generator;

public class PrimitiveImplicitConversionProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "primitive_implicit_conversions";
    public override string Name => "Primitive Implicit Conversions";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var il2CppMscorlib = appContext.Il2CppMscorlib;
        var mscorlib = appContext.Mscorlib;

        // Il2CppSystem.String
        {
            var il2CppType = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.String");
            var monoType = mscorlib.GetTypeByFullNameOrThrow("System.String");
            var typeInfo = il2CppType.GetExtraData<Il2CppTypeInfo>()!;
            foreach (var instanceField in typeInfo.InstanceFields)
            {
                il2CppType.Fields.Remove(instanceField);
            }
            typeInfo.InstanceFields.Clear();

            var objectPointerType = appContext.ResolveTypeOrThrow(typeof(ObjectPointer));
            var objectPointerConversionFromIntPtr = objectPointerType.GetExplicitConversionFrom(appContext.SystemTypes.SystemIntPtrType);

            var il2CppStaticType = appContext.ResolveTypeOrThrow(typeof(IL2CPP));
            var managedStringToIl2Cpp = il2CppStaticType.Methods.First(m => m.Name == nameof(IL2CPP.ManagedStringToIl2Cpp));
            var il2CppObjectBaseToPointer = il2CppStaticType.Methods.First(m => m.Name == nameof(IL2CPP.Il2CppObjectBaseToPtr));
            var il2CppStringToManaged = il2CppStaticType.Methods.First(m => m.Name == nameof(IL2CPP.Il2CppStringToManaged));

            // Il2Cpp -> Mono
            {
                var implicitConversion = new InjectedMethodAnalysisContext(
                    il2CppType,
                    "op_Implicit",
                    monoType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
                    [il2CppType]);
                implicitConversion.IsInjected = true;
                il2CppType.Methods.Add(implicitConversion);

                var createStringNop = new Instruction(OpCodes.Nop);

                implicitConversion.PutExtraData(new TranslatedMethodBody()
                {
                    Instructions = [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Call, il2CppObjectBaseToPointer),
                        new Instruction(OpCodes.Dup),
                        new Instruction(OpCodes.Brtrue, createStringNop),
                        new Instruction(OpCodes.Pop),
                        new Instruction(OpCodes.Ldnull),
                        new Instruction(OpCodes.Ret),
                        createStringNop,
                        new Instruction(OpCodes.Call, il2CppStringToManaged),
                        new Instruction(OpCodes.Ret),
                    ]
                });
            }

            // Mono -> Il2Cpp
            {
                var implicitConversion = new InjectedMethodAnalysisContext(
                    il2CppType,
                    "op_Implicit",
                    il2CppType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
                    [monoType]);
                implicitConversion.IsInjected = true;
                il2CppType.Methods.Add(implicitConversion);

                var createIl2CppStringNop = new Instruction(OpCodes.Nop);

                implicitConversion.PutExtraData(new TranslatedMethodBody()
                {
                    Instructions = [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Dup),
                        new Instruction(OpCodes.Brtrue, createIl2CppStringNop),
                        new Instruction(OpCodes.Pop),
                        new Instruction(OpCodes.Ldnull),
                        new Instruction(OpCodes.Ret),
                        createIl2CppStringNop,
                        new Instruction(OpCodes.Call, managedStringToIl2Cpp),
                        new Instruction(OpCodes.Call, objectPointerConversionFromIntPtr),
                        new Instruction(OpCodes.Newobj, il2CppType.PointerConstructor!),
                        new Instruction(OpCodes.Ret),
                    ]
                });
            }
        }

        ReadOnlySpan<(string, string)> numericPairs =
        [
            ("Il2CppSystem.SByte", "System.SByte"),
            ("Il2CppSystem.Byte", "System.Byte"),
            ("Il2CppSystem.Int16", "System.Int16"),
            ("Il2CppSystem.UInt16", "System.UInt16"),
            ("Il2CppSystem.Int32", "System.Int32"),
            ("Il2CppSystem.UInt32", "System.UInt32"),
            ("Il2CppSystem.Int64", "System.Int64"),
            ("Il2CppSystem.UInt64", "System.UInt64"),
            ("Il2CppSystem.Single", "System.Single"),
            ("Il2CppSystem.Double", "System.Double"),
            ("Il2CppSystem.Char", "System.Char"),
            ("Il2CppSystem.Boolean", "System.Boolean"),
            ("Il2CppSystem.IntPtr", "System.IntPtr"),
            ("Il2CppSystem.UIntPtr", "System.UIntPtr"),
        ];

        foreach (var (il2CppTypeName, monoTypeName) in numericPairs)
        {
            var il2CppType = il2CppMscorlib.GetTypeByFullNameOrThrow(il2CppTypeName);
            var monoType = mscorlib.GetTypeByFullNameOrThrow(monoTypeName);
            var typeInfo = il2CppType.GetExtraData<Il2CppTypeInfo>()!;
            Debug.Assert(typeInfo.InstanceFields.Count == 1, $"Expected exactly one instance field for {il2CppTypeName}");
            var field = typeInfo.InstanceFields[0];
            field.OverrideFieldType = monoType;

            // Il2Cpp -> Mono
            {
                var implicitConversion = new InjectedMethodAnalysisContext(
                    il2CppType,
                    "op_Implicit",
                    monoType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
                    [il2CppType]);
                implicitConversion.IsInjected = true;
                implicitConversion.PutExtraData(new TranslatedMethodBody()
                {
                    Instructions = [
                        new Instruction(OpCodes.Ldarg, implicitConversion.Parameters[0]),
                        new Instruction(OpCodes.Ldfld, field),
                        new Instruction(OpCodes.Ret),
                    ]
                });
                il2CppType.Methods.Add(implicitConversion);
            }

            // Mono -> Il2Cpp
            {
                var implicitConversion = new InjectedMethodAnalysisContext(
                    il2CppType,
                    "op_Implicit",
                    il2CppType,
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.Static,
                    [monoType]);
                implicitConversion.IsInjected = true;
                implicitConversion.PutExtraData(new TranslatedMethodBody()
                {
                    Instructions = [
                        new Instruction(OpCodes.Ldarga, implicitConversion.Parameters[0]),
                        new Instruction(OpCodes.Conv_U),
                        new Instruction(OpCodes.Ldobj, il2CppType),
                        new Instruction(OpCodes.Ret),
                    ]
                });
                il2CppType.Methods.Add(implicitConversion);
            }

            // Mono -> Il2Cpp, ByRef
            {
                // Might not be necessary

                var method = new InjectedMethodAnalysisContext(
                    il2CppType,
                    "ConvertReference",
                    il2CppType.MakeByReferenceType(),
                    MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Static,
                    [monoType.MakeByReferenceType()]);
                method.IsInjected = true;
                method.PutExtraData(new TranslatedMethodBody()
                {
                    Instructions = [
                        new Instruction(OpCodes.Ldarg_0),
                        new Instruction(OpCodes.Ret),
                    ]
                });
                il2CppType.Methods.Add(method);
            }
        }
    }
    
}
