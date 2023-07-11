using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Extensions;

public static class ILGeneratorEx
{
    private static readonly OpCode[] I4Constants =
    {
        OpCodes.Ldc_I4_M1,
        OpCodes.Ldc_I4_0,
        OpCodes.Ldc_I4_1,
        OpCodes.Ldc_I4_2,
        OpCodes.Ldc_I4_3,
        OpCodes.Ldc_I4_4,
        OpCodes.Ldc_I4_5,
        OpCodes.Ldc_I4_6,
        OpCodes.Ldc_I4_7,
        OpCodes.Ldc_I4_8
    };

    public static void EmitLdcI4(this ILProcessor body, int constant)
    {
        if (constant >= -1 && constant <= 8)
            body.Emit(I4Constants[constant + 1]);
        else if (constant >= byte.MinValue && constant <= byte.MaxValue)
            body.Emit(OpCodes.Ldc_I4_S, (sbyte)constant);
        else
            body.Emit(OpCodes.Ldc_I4, constant);
    }

    public static void EmitObjectStore(this ILProcessor body, TypeReference originalType, TypeReference newType,
        TypeRewriteContext enclosingType, int argumentIndex)
    {
        // input stack: object address, target address
        // output: nothing
        if (originalType is GenericParameter)
        {
            EmitObjectStoreGeneric(body, originalType, newType, enclosingType, argumentIndex);
            return;
        }

        var imports = enclosingType.AssemblyContext.Imports;

        if (originalType.FullName == "System.String")
        {
            body.Emit(OpCodes.Ldarg, argumentIndex);
            body.Emit(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
            body.Emit(OpCodes.Call, imports.WriteFieldWBarrier);
        }
        else if (originalType.IsValueType)
        {
            var typeSpecifics = enclosingType.AssemblyContext.GlobalContext.JudgeSpecificsByOriginalType(originalType);
            if (typeSpecifics == TypeRewriteContext.TypeSpecifics.BlittableStruct)
            {
                body.Emit(OpCodes.Ldarg, argumentIndex);
                body.Emit(OpCodes.Stobj, newType);
                body.Emit(OpCodes.Pop);
            }
            else
            {
                body.Emit(OpCodes.Ldarg, argumentIndex);
                body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
                body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
                var classPointerTypeRef = new GenericInstanceType(imports.Il2CppClassPointerStore)
                { GenericArguments = { newType } };
                var classPointerFieldRef =
                    new FieldReference("NativeClassPtr", imports.Module.IntPtr(), classPointerTypeRef);
                body.Emit(OpCodes.Ldsfld, enclosingType.NewType.Module.ImportReference(classPointerFieldRef));
                body.Emit(OpCodes.Ldc_I4_0);
                body.Emit(OpCodes.Conv_U);
                body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_class_value_size.Value);
                body.Emit(OpCodes.Cpblk);
                body.Emit(OpCodes.Pop);
            }
        }
        else
        {
            body.Emit(OpCodes.Ldarg, argumentIndex);
            body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
            body.Emit(OpCodes.Call, imports.WriteFieldWBarrier);
        }
    }

    private static void EmitObjectStoreGeneric(ILProcessor body, TypeReference originalType, TypeReference newType,
        TypeRewriteContext enclosingType, int argumentIndex)
    {
        // input stack: object address, target address
        // output: nothing

        var imports = enclosingType.AssemblyContext.Imports;

        body.Emit(OpCodes.Ldtoken, newType);
        body.Emit(OpCodes.Call, enclosingType.NewType.Module.TypeGetTypeFromHandle());
        body.Emit(OpCodes.Dup);
        body.Emit(OpCodes.Callvirt, enclosingType.NewType.Module.TypeGetIsValueType());

        var finalNop = body.Create(OpCodes.Nop);
        var stringNop = body.Create(OpCodes.Nop);
        var valueTypeNop = body.Create(OpCodes.Nop);
        var storePointerNop = body.Create(OpCodes.Nop);

        body.Emit(OpCodes.Brtrue, valueTypeNop);

        body.Emit(OpCodes.Callvirt, enclosingType.NewType.Module.TypeGetFullName());
        body.Emit(OpCodes.Ldstr, "System.String");
        body.Emit(OpCodes.Call, enclosingType.NewType.Module.StringEquals());
        body.Emit(OpCodes.Brtrue_S, stringNop);

        body.Emit(OpCodes.Ldarg, argumentIndex);
        body.Emit(OpCodes.Box, newType);
        body.Emit(OpCodes.Isinst, imports.Il2CppObjectBase);
        body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
        body.Emit(OpCodes.Dup);
        body.Emit(OpCodes.Brfalse_S, storePointerNop);

        body.Emit(OpCodes.Dup);
        body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_class.Value);
        body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_class_is_valuetype.Value);
        body.Emit(OpCodes.Brfalse_S, storePointerNop);

        body.Emit(OpCodes.Dup);
        var tempLocal = new VariableDefinition(imports.Module.IntPtr());
        body.Body.Variables.Add(tempLocal);
        body.Emit(OpCodes.Stloc, tempLocal);
        body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
        body.Emit(OpCodes.Ldloc, tempLocal);
        body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_class.Value);
        body.Emit(OpCodes.Ldc_I4_0);
        body.Emit(OpCodes.Conv_U);
        body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_class_value_size.Value);
        body.Emit(OpCodes.Cpblk);
        body.Emit(OpCodes.Pop);
        body.Emit(OpCodes.Br_S, finalNop);

        body.Append(storePointerNop);
        body.Emit(OpCodes.Call, imports.WriteFieldWBarrier);
        body.Emit(OpCodes.Br_S, finalNop);

        body.Append(stringNop);
        body.Emit(OpCodes.Ldarg, argumentIndex);
        body.Emit(OpCodes.Box, newType);
        body.Emit(OpCodes.Isinst, imports.Module.String());
        body.Emit(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        body.Emit(OpCodes.Call, imports.WriteFieldWBarrier);
        body.Emit(OpCodes.Br_S, finalNop);

        body.Append(valueTypeNop);
        body.Emit(OpCodes.Pop); // pop extra typeof(T)
        body.Emit(OpCodes.Ldarg, argumentIndex);
        body.Emit(OpCodes.Stobj, newType);
        body.Emit(OpCodes.Pop);

        body.Append(finalNop);
    }

    public static void EmitObjectToPointer(this ILProcessor body, TypeReference originalType, TypeReference newType,
        TypeRewriteContext enclosingType, int argumentIndex, bool valueTypeArgument0IsAPointer, bool allowNullable,
        bool unboxNonBlittableType, bool unboxNonBlittableGeneric, out VariableDefinition? refVariable)
    {
        // input stack: not used
        // output stack: IntPtr to either Il2CppObject or IL2CPP value type
        refVariable = null;

        if (originalType is GenericParameter)
        {
            EmitObjectToPointerGeneric(body, originalType, newType, enclosingType, argumentIndex,
                valueTypeArgument0IsAPointer, allowNullable, unboxNonBlittableGeneric);
            return;
        }

        var imports = enclosingType.AssemblyContext.Imports;
        if (originalType is ByReferenceType)
        {
            if (newType.GetElementType().IsValueType)
            {
                body.Emit(OpCodes.Ldarg, argumentIndex);
                body.Emit(OpCodes.Conv_I);
            }
            else if (originalType.GetElementType().IsValueType)
            {
                body.Emit(OpCodes.Ldarg, argumentIndex);
                body.Emit(OpCodes.Ldind_Ref);
                body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
            }
            else
            {
                var pointerVar = new VariableDefinition(imports.Module.IntPtr());
                refVariable = pointerVar;
                body.Body.Variables.Add(pointerVar);
                body.Emit(OpCodes.Ldarg, argumentIndex);
                body.Emit(OpCodes.Ldind_Ref);
                if (originalType.GetElementType().FullName == "System.String")
                    body.Emit(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
                else
                    body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtr.Value);
                body.Emit(OpCodes.Stloc, pointerVar);
                body.Emit(OpCodes.Ldloca, pointerVar);
                body.Emit(OpCodes.Conv_I);
            }
        }
        else if (originalType.IsValueType)
        {
            if (newType.IsValueType)
            {
                if (argumentIndex == 0 && valueTypeArgument0IsAPointer)
                    body.Emit(OpCodes.Ldarg_0);
                else
                    body.Emit(OpCodes.Ldarga, argumentIndex);
            }
            else
            {
                body.Emit(OpCodes.Ldarg, argumentIndex);
                body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
                if (unboxNonBlittableType)
                    body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
            }
        }
        else if (originalType.FullName == "System.String")
        {
            body.Emit(OpCodes.Ldarg, argumentIndex);
            body.Emit(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        }
        else if (originalType.IsPointer)
        {
            body.Emit(OpCodes.Ldarg, argumentIndex);
            body.Emit(OpCodes.Call, new MethodReference("op_Explicit", imports.Module.IntPtr(), imports.Module.IntPtr())
            { Parameters = { new ParameterDefinition(imports.Module.ImportReference(typeof(void*))) } });
        }
        else
        {
            body.Emit(OpCodes.Ldarg, argumentIndex);
            body.Emit(OpCodes.Call,
                allowNullable
                    ? imports.IL2CPP_Il2CppObjectBaseToPtr.Value
                    : imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
        }
    }

    private static void EmitObjectToPointerGeneric(ILProcessor body, TypeReference originalType,
        TypeReference newType, TypeRewriteContext enclosingType, int argumentIndex,
        bool valueTypeArgument0IsAPointer, bool allowNullable, bool unboxNonBlittableType)
    {
        var imports = enclosingType.AssemblyContext.Imports;

        body.Emit(OpCodes.Ldtoken, newType);
        body.Emit(OpCodes.Call, enclosingType.NewType.Module.TypeGetTypeFromHandle());
        body.Emit(OpCodes.Callvirt, enclosingType.NewType.Module.TypeGetIsValueType());

        var finalNop = body.Create(OpCodes.Nop);
        var valueTypeNop = body.Create(OpCodes.Nop);
        var stringNop = body.Create(OpCodes.Nop);

        body.Emit(OpCodes.Brtrue, valueTypeNop);

        body.Emit(OpCodes.Ldarg, argumentIndex);
        body.Emit(OpCodes.Box, newType);
        body.Emit(OpCodes.Dup);
        body.Emit(OpCodes.Isinst, imports.Module.String());
        body.Emit(OpCodes.Brtrue_S, stringNop);

        body.Emit(OpCodes.Isinst, imports.Il2CppObjectBase);
        body.Emit(OpCodes.Call,
            allowNullable
                ? imports.IL2CPP_Il2CppObjectBaseToPtr.Value
                : imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);
        if (unboxNonBlittableType)
        {
            body.Emit(OpCodes.Dup);
            body.Emit(OpCodes.Brfalse_S, finalNop); // return null immediately
            body.Emit(OpCodes.Dup);
            body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_get_class.Value);
            body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_class_is_valuetype.Value);
            body.Emit(OpCodes.Brfalse_S, finalNop); // return reference types immediately
            body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
        }

        body.Emit(OpCodes.Br, finalNop);

        body.Append(stringNop);
        body.Emit(OpCodes.Isinst, imports.Module.String());
        body.Emit(OpCodes.Call, imports.IL2CPP_ManagedStringToIl2Cpp.Value);
        body.Emit(OpCodes.Br_S, finalNop);

        body.Append(valueTypeNop);
        body.Emit(OpCodes.Ldarga, argumentIndex);

        body.Append(finalNop);
    }

    public static void EmitPointerToObject(this ILProcessor body, TypeReference originalReturnType,
        TypeReference convertedReturnType, TypeRewriteContext enclosingType, Instruction loadPointer,
        bool extraDerefForNonValueTypes, bool unboxValueType)
    {
        // input stack: not used
        // output stack: converted result

        if (originalReturnType is GenericParameter)
        {
            EmitPointerToObjectGeneric(body, originalReturnType, convertedReturnType, enclosingType, loadPointer,
                extraDerefForNonValueTypes, unboxValueType);
            return;
        }

        var imports = enclosingType.AssemblyContext.Imports;
        if (originalReturnType.FullName == "System.Void")
        {
            // do nothing
        }
        else if (originalReturnType.IsValueType)
        {
            if (convertedReturnType.IsValueType)
            {
                body.Append(loadPointer);
                if (unboxValueType) body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_object_unbox.Value);
                body.Emit(OpCodes.Ldobj, convertedReturnType);
            }
            else
            {
                if (unboxValueType)
                {
                    body.Append(loadPointer);
                }
                else
                {
                    var classPointerTypeRef = new GenericInstanceType(imports.Il2CppClassPointerStore)
                    { GenericArguments = { convertedReturnType } };
                    var classPointerFieldRef =
                        new FieldReference("NativeClassPtr", imports.Module.IntPtr(),
                            classPointerTypeRef);
                    body.Emit(OpCodes.Ldsfld, enclosingType.NewType.Module.ImportReference(classPointerFieldRef));
                    body.Append(loadPointer);
                    body.Emit(OpCodes.Call, imports.IL2CPP_il2cpp_value_box.Value);
                }

                body.Emit(OpCodes.Newobj,
                    new MethodReference(".ctor", imports.Module.Void(), convertedReturnType)
                    { Parameters = { new ParameterDefinition(imports.Module.IntPtr()) }, HasThis = true });
            }
        }
        else if (originalReturnType.FullName == "System.String")
        {
            body.Append(loadPointer);
            if (extraDerefForNonValueTypes) body.Emit(OpCodes.Ldind_I);
            body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppStringToManaged.Value);
        }
        else if (originalReturnType.IsArray && originalReturnType.GetElementType().IsGenericParameter)
        {
            body.Append(loadPointer);
            if (extraDerefForNonValueTypes) body.Emit(OpCodes.Ldind_I);
            var actualReturnType = imports.Module.ImportReference(new GenericInstanceType(imports.Il2CppArrayBase)
            { GenericArguments = { imports.Il2CppArrayBase.GenericParameters[0] } });
            var methodRef = new MethodReference("WrapNativeGenericArrayPointer",
                    actualReturnType,
                    convertedReturnType)
            { HasThis = false, Parameters = { new ParameterDefinition(imports.Module.IntPtr()) } };
            body.Emit(OpCodes.Call, methodRef);
        }
        else if (originalReturnType.IsPointer)
        {
            body.Append(loadPointer);
            body.Emit(OpCodes.Call,
                new MethodReference("op_Explicit", imports.Module.ImportReference(typeof(void*)), imports.Module.IntPtr())
                { Parameters = { new ParameterDefinition(imports.Module.IntPtr()) } });
        }
        else
        {
            var createPoolObject = body.Create(OpCodes.Call,
                imports.Module.ImportReference(new GenericInstanceMethod(imports.Il2CppObjectPool_Get.Value)
                { GenericArguments = { convertedReturnType } }));
            var endNop = body.Create(OpCodes.Nop);

            body.Append(loadPointer);
            if (extraDerefForNonValueTypes) body.Emit(OpCodes.Ldind_I);
            body.Emit(OpCodes.Dup);
            body.Emit(OpCodes.Brtrue_S, createPoolObject);
            body.Emit(OpCodes.Pop);
            body.Emit(OpCodes.Ldnull);
            body.Emit(OpCodes.Br, endNop);

            body.Append(createPoolObject);
            body.Append(endNop);
        }
    }

    private static void EmitPointerToObjectGeneric(ILProcessor body, TypeReference originalReturnType,
        TypeReference newReturnType,
        TypeRewriteContext enclosingType, Instruction loadPointer, bool extraDerefForNonValueTypes,
        bool unboxValueType)
    {
        var imports = enclosingType.AssemblyContext.Imports;

        body.Append(loadPointer);

        body.Emit(extraDerefForNonValueTypes ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
        body.Emit(unboxValueType ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);

        body.Emit(OpCodes.Call,
            imports.Module.ImportReference(new GenericInstanceMethod(imports.IL2CPP_PointerToValueGeneric.Value)
            { GenericArguments = { newReturnType } }));
    }

    public static void GenerateBoxMethod(RuntimeAssemblyReferences imports, TypeDefinition targetType,
        FieldReference classHandle, TypeReference il2CppObjectTypeDef)
    {
        var method = new MethodDefinition("BoxIl2CppObject", MethodAttributes.Public | MethodAttributes.HideBySig,
            targetType.Module.ImportReference(il2CppObjectTypeDef));
        targetType.Methods.Add(method);

        var methodBody = method.Body.GetILProcessor();
        methodBody.Emit(OpCodes.Ldsfld, classHandle);
        methodBody.Emit(OpCodes.Ldarg_0);
        methodBody.Emit(OpCodes.Call, targetType.Module.ImportReference(imports.IL2CPP_il2cpp_value_box.Value));

        methodBody.Emit(OpCodes.Newobj,
            new MethodReference(".ctor", targetType.Module.Void(), il2CppObjectTypeDef)
            { Parameters = { new ParameterDefinition(targetType.Module.IntPtr()) }, HasThis = true });

        methodBody.Emit(OpCodes.Ret);
    }

    public static void EmitUpdateRef(this ILProcessor body, ParameterDefinition newMethodParameter, int argIndex,
        VariableDefinition paramVariable, RuntimeAssemblyReferences imports)
    {
        body.Emit(OpCodes.Ldarg, argIndex);
        body.Emit(OpCodes.Ldloc, paramVariable);
        if (newMethodParameter.ParameterType.GetElementType().FullName == "System.String")
        {
            body.Emit(OpCodes.Call, imports.IL2CPP_Il2CppStringToManaged.Value);
        }
        else
        {
            body.Emit(OpCodes.Dup);
            var nullbr = body.Create(OpCodes.Pop);
            var stnop = body.Create(OpCodes.Nop);
            body.Emit(OpCodes.Brfalse_S, nullbr);

            if (newMethodParameter.ParameterType.GetElementType() is GenericParameter)
            {
                body.Emit(OpCodes.Ldc_I4_0);
                body.Emit(OpCodes.Ldc_I4_0);
                body.Emit(OpCodes.Call,
                    imports.Module.ImportReference(new GenericInstanceMethod(imports.IL2CPP_PointerToValueGeneric.Value)
                    { GenericArguments = { newMethodParameter.ParameterType.GetElementType() } }));
            }
            else
            {
                body.Emit(OpCodes.Newobj,
                    new MethodReference(".ctor", imports.Module.Void(), newMethodParameter.ParameterType.GetElementType())
                    { HasThis = true, Parameters = { new ParameterDefinition(imports.Module.IntPtr()) } });
            }
            body.Emit(OpCodes.Br_S, stnop);

            body.Append(nullbr);
            body.Emit(OpCodes.Ldnull);
            body.Append(stnop);
        }

        body.Emit(OpCodes.Stind_Ref);
    }
}
