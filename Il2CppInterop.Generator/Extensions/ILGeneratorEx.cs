using System.Diagnostics;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Extensions;

public static class ILGeneratorEx
{
    public static void EmitObjectStore(this ILProcessor body, TypeSignature originalType, TypeSignature newType,
        TypeRewriteContext enclosingType, int argumentIndex)
    {
        // input stack: object address, target address
        // output: nothing
        if (originalType is GenericParameterSignature)
        {
            EmitObjectStoreGeneric(body, originalType, newType, enclosingType, argumentIndex);
            return;
        }

        var imports = enclosingType.AssemblyContext.Imports;

        if (originalType.FullName == "System.String")
        {
            body.AddLoadArgument(argumentIndex);
            body.Add(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
            body.Add(OpCodes.Call, imports.WriteFieldWBarrier);
        }
        else if (originalType.IsPointerLike())
        {
            Debug.Assert(newType.IsPointerLike());
            body.AddLoadArgument(argumentIndex);
            body.Add(OpCodes.Stobj, newType.ToTypeDefOrRef());
            body.Add(OpCodes.Pop);
        }
        else if (originalType.IsValueType)
        {
            var typeSpecifics = enclosingType.AssemblyContext.GlobalContext.JudgeSpecificsByOriginalType(originalType);
            if (typeSpecifics == TypeRewriteContext.TypeSpecifics.BlittableStruct)
            {
                body.AddLoadArgument(argumentIndex);
                body.Add(OpCodes.Stobj, newType.ToTypeDefOrRef());
                body.Add(OpCodes.Pop);
            }
            else
            {
                body.AddLoadArgument(argumentIndex);
                body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
                body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
                var classPointerTypeRef = new GenericInstanceTypeSignature(imports.Il2CppClassPointerStore.ToTypeDefOrRef(), imports.Il2CppClassPointerStore.IsValueType, newType);
                var classPointerFieldRef =
                    ReferenceCreator.CreateFieldReference("NativeClassPtr", imports.Module.IntPtr(), classPointerTypeRef.ToTypeDefOrRef());
                body.Add(OpCodes.Ldsfld, enclosingType.NewType.Module!.DefaultImporter.ImportField(classPointerFieldRef));
                body.Add(OpCodes.Ldc_I4_0);
                body.Add(OpCodes.Conv_U);
                body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_class_value_size.Value);
                body.Add(OpCodes.Cpblk);
                body.Add(OpCodes.Pop);
            }
        }
        else
        {
            body.AddLoadArgument(argumentIndex);
            body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
            body.Add(OpCodes.Call, imports.WriteFieldWBarrier);
        }
    }

    private static void EmitObjectStoreGeneric(ILProcessor body, TypeSignature originalType, TypeSignature newType,
        TypeRewriteContext enclosingType, int argumentIndex)
    {
        // input stack: object address, target address
        // output: nothing

        var imports = enclosingType.AssemblyContext.Imports;

        Debug.Assert(enclosingType.NewType.Module is not null);
        body.Add(OpCodes.Ldtoken, newType.ToTypeDefOrRef());
        body.Add(OpCodes.Call, enclosingType.NewType.Module!.TypeGetTypeFromHandle());
        body.Add(OpCodes.Dup);
        body.Add(OpCodes.Callvirt, enclosingType.NewType.Module!.TypeGetIsValueType());

        var finalNop = new CilInstructionLabel();
        var stringNop = new CilInstructionLabel();
        var valueTypeNop = new CilInstructionLabel();
        var storePointerNop = new CilInstructionLabel();

        body.Add(OpCodes.Brtrue, valueTypeNop);

        body.Add(OpCodes.Callvirt, enclosingType.NewType.Module!.TypeGetFullName());
        body.Add(OpCodes.Ldstr, "System.String");
        body.Add(OpCodes.Call, enclosingType.NewType.Module!.StringEquals());
        body.Add(OpCodes.Brtrue_S, stringNop);

        body.AddLoadArgument(argumentIndex);
        body.Add(OpCodes.Box, newType.ToTypeDefOrRef());
        body.Add(OpCodes.Isinst, imports.Il2CppObjectBase.ToTypeDefOrRef());
        body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
        body.Add(OpCodes.Dup);
        body.Add(OpCodes.Brfalse_S, storePointerNop);

        body.Add(OpCodes.Dup);
        body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_class.Value);
        body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_class_is_valuetype.Value);
        body.Add(OpCodes.Brfalse_S, storePointerNop);

        body.Add(OpCodes.Dup);
        var tempLocal = new CilLocalVariable(imports.Module.IntPtr());
        body.Owner.LocalVariables.Add(tempLocal);
        body.Add(OpCodes.Stloc, tempLocal);
        body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
        body.Add(OpCodes.Ldloc, tempLocal);
        body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_class.Value);
        body.Add(OpCodes.Ldc_I4_0);
        body.Add(OpCodes.Conv_U);
        body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_class_value_size.Value);
        body.Add(OpCodes.Cpblk);
        body.Add(OpCodes.Pop);
        body.Add(OpCodes.Br_S, finalNop);

        storePointerNop.Instruction = body.Add(OpCodes.Nop);
        body.Add(OpCodes.Call, imports.WriteFieldWBarrier);
        body.Add(OpCodes.Br_S, finalNop);

        stringNop.Instruction = body.Add(OpCodes.Nop);
        body.AddLoadArgument(argumentIndex);
        body.Add(OpCodes.Box, newType.ToTypeDefOrRef());
        body.Add(OpCodes.Isinst, imports.Module.String().ToTypeDefOrRef());
        body.Add(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        body.Add(OpCodes.Call, imports.WriteFieldWBarrier);
        body.Add(OpCodes.Br_S, finalNop);

        valueTypeNop.Instruction = body.Add(OpCodes.Nop);
        body.Add(OpCodes.Pop); // pop extra typeof(T)
        body.AddLoadArgument(argumentIndex);
        body.Add(OpCodes.Stobj, newType.ToTypeDefOrRef());
        body.Add(OpCodes.Pop);

        finalNop.Instruction = body.Add(OpCodes.Nop);
    }

    public static void EmitObjectToPointer(this ILProcessor body, TypeSignature originalType, TypeSignature newType,
        TypeRewriteContext enclosingType, int argumentIndex, bool valueTypeArgument0IsAPointer, bool allowNullable,
        bool unboxNonBlittableType, bool unboxNonBlittableGeneric, out CilLocalVariable? refVariable)
    {
        // input stack: not used
        // output stack: IntPtr to either Il2CppObject or IL2CPP value type
        refVariable = null;

        if (originalType is GenericParameterSignature)
        {
            EmitObjectToPointerGeneric(body, originalType, newType, enclosingType, argumentIndex,
                valueTypeArgument0IsAPointer, allowNullable, unboxNonBlittableGeneric);
            return;
        }

        var imports = enclosingType.AssemblyContext.Imports;
        if (originalType is ByReferenceTypeSignature)
        {
            if (newType.GetElementType().IsValueType)
            {
                body.AddLoadArgument(argumentIndex);
                body.Add(OpCodes.Conv_I);
            }
            else if (originalType.GetElementType().IsValueType)
            {
                body.AddLoadArgument(argumentIndex);
                body.Add(OpCodes.Ldind_Ref);
                body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
            }
            else
            {
                var pointerVar = new CilLocalVariable(imports.Module.IntPtr());
                refVariable = pointerVar;
                body.Owner.LocalVariables.Add(pointerVar);
                body.AddLoadArgument(argumentIndex);
                body.Add(OpCodes.Ldind_Ref);
                if (originalType.GetElementType().FullName == "System.String")
                    body.Add(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
                else
                    body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
                body.Add(OpCodes.Stloc, pointerVar);
                body.Add(OpCodes.Ldloca, pointerVar);
                body.Add(OpCodes.Conv_I);
            }
        }
        else if (originalType.IsPointerLike())
        {
            Debug.Assert(newType.IsPointerLike());
            body.AddLoadArgument(argumentIndex);
        }
        else if (originalType.IsValueType)
        {
            if (newType.IsValueType)
            {
                if (argumentIndex == 0 && valueTypeArgument0IsAPointer)
                    body.Add(OpCodes.Ldarg_0);
                else
                    body.AddLoadArgumentAddress(argumentIndex);
            }
            else
            {
                body.AddLoadArgument(argumentIndex);
                body.Add(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
                if (unboxNonBlittableType)
                    body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
            }
        }
        else if (originalType.FullName == "System.String")
        {
            body.AddLoadArgument(argumentIndex);
            body.Add(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        }
        else
        {
            body.AddLoadArgument(argumentIndex);
            body.Add(OpCodes.Call,
                allowNullable
                    ? imports.IL2CPP_Il2CppObjectBaseToPtr.Value
                    : imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
        }
    }

    private static void EmitObjectToPointerGeneric(ILProcessor body, TypeSignature originalType,
        TypeSignature newType, TypeRewriteContext enclosingType, int argumentIndex,
        bool valueTypeArgument0IsAPointer, bool allowNullable, bool unboxNonBlittableType)
    {
        var imports = enclosingType.AssemblyContext.Imports;

        Debug.Assert(enclosingType.NewType.Module is not null);
        body.Add(OpCodes.Ldtoken, newType.ToTypeDefOrRef());
        body.Add(OpCodes.Call, enclosingType.NewType.Module!.TypeGetTypeFromHandle());
        body.Add(OpCodes.Callvirt, enclosingType.NewType.Module!.TypeGetIsValueType());

        var finalNop = new CilInstructionLabel();
        var valueTypeNop = new CilInstructionLabel();
        var stringNop = new CilInstructionLabel();

        body.Add(OpCodes.Brtrue, valueTypeNop);

        body.AddLoadArgument(argumentIndex);
        body.Add(OpCodes.Box, newType.ToTypeDefOrRef());
        body.Add(OpCodes.Dup);
        body.Add(OpCodes.Isinst, imports.Module.String().ToTypeDefOrRef());
        body.Add(OpCodes.Brtrue_S, stringNop);

        body.Add(OpCodes.Isinst, imports.Il2CppObjectBase.ToTypeDefOrRef());
        body.Add(OpCodes.Call,
            allowNullable
                ? imports.IL2CPP_Il2CppObjectBaseToPtr.Value
                : imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
        if (unboxNonBlittableType)
        {
            body.Add(OpCodes.Dup);
            body.Add(OpCodes.Brfalse_S, finalNop); // return null immediately
            body.Add(OpCodes.Dup);
            body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_class.Value);
            body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_class_is_valuetype.Value);
            body.Add(OpCodes.Brfalse_S, finalNop); // return reference types immediately
            body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
        }

        body.Add(OpCodes.Br, finalNop);

        stringNop.Instruction = body.Add(OpCodes.Nop);
        body.Add(OpCodes.Isinst, imports.Module.String().ToTypeDefOrRef());
        body.Add(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        body.Add(OpCodes.Br_S, finalNop);

        valueTypeNop.Instruction = body.Add(OpCodes.Nop);
        body.AddLoadArgumentAddress(argumentIndex);

        finalNop.Instruction = body.Add(OpCodes.Nop);
    }

    public static void EmitPointerToObject(this ILProcessor body, TypeSignature originalReturnType,
        TypeSignature convertedReturnType, TypeRewriteContext enclosingType, CilLocalVariable pointerVariable,
        bool extraDerefForNonValueTypes, bool unboxValueType)
    {
        // input stack: not used
        // output stack: converted result

        if (originalReturnType is GenericParameterSignature)
        {
            EmitPointerToObjectGeneric(body, originalReturnType, convertedReturnType, enclosingType, pointerVariable,
                extraDerefForNonValueTypes, unboxValueType);
            return;
        }

        var imports = enclosingType.AssemblyContext.Imports;
        if (originalReturnType.FullName == "System.Void")
        {
            // do nothing
        }
        else if (originalReturnType.IsPointerLike())
        {
            Debug.Assert(convertedReturnType.IsPointerLike());
            body.Add(OpCodes.Ldloc, pointerVariable);
        }
        else if (originalReturnType.IsValueType)
        {
            if (convertedReturnType.IsValueType)
            {
                body.Add(OpCodes.Ldloc, pointerVariable);
                if (unboxValueType) body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
                body.Add(OpCodes.Ldobj, convertedReturnType.ToTypeDefOrRef());
            }
            else
            {
                if (unboxValueType)
                {
                    body.Add(OpCodes.Ldloc, pointerVariable);
                }
                else
                {
                    Debug.Assert(enclosingType.NewType.Module is not null);
                    var classPointerTypeRef = new GenericInstanceTypeSignature(imports.Il2CppClassPointerStore.ToTypeDefOrRef(), imports.Il2CppClassPointerStore.IsValueType, convertedReturnType);
                    var classPointerFieldRef =
                        ReferenceCreator.CreateFieldReference("NativeClassPtr", imports.Module.IntPtr(),
                            classPointerTypeRef.ToTypeDefOrRef());
                    body.Add(OpCodes.Ldsfld, enclosingType.NewType.Module!.DefaultImporter.ImportField(classPointerFieldRef));
                    body.Add(OpCodes.Ldloc, pointerVariable);
                    body.Add(OpCodes.Call, imports.IL2CPP_il2cpp_value_box.Value);
                }

                body.Add(OpCodes.Newobj,
                    ReferenceCreator.CreateInstanceMethodReference(".ctor", imports.Module.Void(), convertedReturnType.ToTypeDefOrRef(), imports.Module.IntPtr()));
            }
        }
        else if (originalReturnType.FullName == "System.String")
        {
            body.Add(OpCodes.Ldloc, pointerVariable);
            if (extraDerefForNonValueTypes) body.Add(OpCodes.Ldind_I);
            body.Add(OpCodes.Call, imports.IL2CPP_Il2CppStringToManaged.Value);
        }
        else if (originalReturnType is ArrayBaseTypeSignature && originalReturnType.GetElementType() is GenericParameterSignature genericParameterSignature)
        {
            // Note:
            // The method reference parent is constructed relative to the calling method.
            // The return type and parameter types are constructed relative to the called method.
            body.Add(OpCodes.Ldloc, pointerVariable);
            if (extraDerefForNonValueTypes) body.Add(OpCodes.Ldind_I);
            var actualReturnType = imports.Module.DefaultImporter.ImportTypeSignature(imports.Il2CppArrayBase.MakeGenericInstanceType(new GenericParameterSignature(GenericParameterType.Type, 0)));
            var methodRef = ReferenceCreator.CreateStaticMethodReference("WrapNativeGenericArrayPointer",
                    actualReturnType,
                    convertedReturnType.ToTypeDefOrRef(),
                    imports.Module.IntPtr());
            body.Add(OpCodes.Call, methodRef);
        }
        else
        {
            var createPoolObject = new CilInstructionLabel();
            var endNop = new CilInstructionLabel();

            body.Add(OpCodes.Ldloc, pointerVariable);
            if (extraDerefForNonValueTypes) body.Add(OpCodes.Ldind_I);
            body.Add(OpCodes.Dup);
            body.Add(OpCodes.Brtrue_S, createPoolObject);
            body.Add(OpCodes.Pop);
            body.Add(OpCodes.Ldnull);
            body.Add(OpCodes.Br, endNop);

            createPoolObject.Instruction = body.Add(OpCodes.Call,
                imports.Module.DefaultImporter.ImportMethod(imports.Il2CppObjectPool_Get.Value.MakeGenericInstanceMethod(convertedReturnType)));
            endNop.Instruction = body.Add(OpCodes.Nop);
        }
    }

    private static void EmitPointerToObjectGeneric(ILProcessor body, TypeSignature originalReturnType,
        TypeSignature newReturnType,
        TypeRewriteContext enclosingType, CilLocalVariable pointerVariable, bool extraDerefForNonValueTypes,
        bool unboxValueType)
    {
        var imports = enclosingType.AssemblyContext.Imports;

        body.Add(OpCodes.Ldloc, pointerVariable);

        body.Add(extraDerefForNonValueTypes ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        body.Add(unboxValueType ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        body.Add(OpCodes.Call,
            imports.Module.DefaultImporter.ImportMethod(imports.IL2CPP_PointerToValueGeneric.Value.MakeGenericInstanceMethod(newReturnType)));
    }

    public static void GenerateBoxMethod(RuntimeAssemblyReferences imports, TypeDefinition targetType,
        IFieldDescriptor classHandle, TypeSignature il2CppObjectTypeDef)
    {
        Debug.Assert(targetType.Module is not null);
        var method = new MethodDefinition("BoxIl2CppObject", MethodAttributes.Public | MethodAttributes.HideBySig,
            MethodSignature.CreateInstance(targetType.Module!.DefaultImporter.ImportTypeSignature(il2CppObjectTypeDef)));
        targetType.Methods.Add(method);

        method.CilMethodBody = new CilMethodBody(method);
        var methodBody = method.CilMethodBody.Instructions;
        methodBody.Add(OpCodes.Ldsfld, classHandle);
        methodBody.Add(OpCodes.Ldarg_0);
        methodBody.Add(OpCodes.Call, targetType.Module.DefaultImporter.ImportMethod(imports.IL2CPP_il2cpp_value_box.Value));

        methodBody.Add(OpCodes.Newobj,
            new MemberReference(il2CppObjectTypeDef.ToTypeDefOrRef(), ".ctor", MethodSignature.CreateInstance(targetType.Module.Void(), targetType.Module.IntPtr())));

        methodBody.Add(OpCodes.Ret);
    }

    public static void EmitUpdateRef(this ILProcessor body, Parameter newMethodParameter, int argIndex,
        CilLocalVariable paramVariable, RuntimeAssemblyReferences imports)
    {
        body.AddLoadArgument(argIndex);
        body.Add(OpCodes.Ldloc, paramVariable);
        if (newMethodParameter.ParameterType.GetElementType().FullName == "System.String")
        {
            body.Add(OpCodes.Call, imports.IL2CPP_Il2CppStringToManaged.Value);
        }
        else
        {
            body.Add(OpCodes.Dup);
            var nullbr = new CilInstructionLabel();
            var stnop = new CilInstructionLabel();
            body.Add(OpCodes.Brfalse_S, nullbr);

            if (newMethodParameter.ParameterType.GetElementType() is GenericParameterSignature)
            {
                body.Add(OpCodes.Ldc_I4_0);
                body.Add(OpCodes.Ldc_I4_0);
                body.Add(OpCodes.Call,
                    imports.Module.DefaultImporter.ImportMethod(imports.IL2CPP_PointerToValueGeneric.Value.MakeGenericInstanceMethod(newMethodParameter.ParameterType.GetElementType())));
            }
            else
            {
                body.Add(OpCodes.Newobj,
                    ReferenceCreator.CreateInstanceMethodReference(".ctor", imports.Module.Void(), newMethodParameter.ParameterType.GetElementType().ToTypeDefOrRef(), imports.Module.IntPtr()));
            }
            body.Add(OpCodes.Br_S, stnop);

            nullbr.Instruction = body.Add(OpCodes.Pop);
            body.Add(OpCodes.Ldnull);
            stnop.Instruction = body.Add(OpCodes.Nop);
        }

        body.Add(OpCodes.Stind_Ref);
    }
}
