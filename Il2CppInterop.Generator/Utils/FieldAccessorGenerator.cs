using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Utils;

internal static class FieldAccessorGenerator
{
    public static void MakeGetter(FieldDefinition field, FieldRewriteContext fieldContext, PropertyDefinition property,
        RuntimeAssemblyReferences imports)
    {
        var attributes = Field2MethodAttrs(field.Attributes) | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
        var getter = new MethodDefinition("get_" + property.Name,
            attributes,
            MethodSignatureCreator.CreateMethodSignature(attributes, property.Signature!.ReturnType, 0));

        getter.CilMethodBody = new(getter);
        var getterBody = getter.CilMethodBody.Instructions;
        property.DeclaringType!.Methods.Add(getter);

        CilLocalVariable local0;
        if (field.IsStatic)
        {
            local0 = new CilLocalVariable(property.Signature.ReturnType.IsValueType
                ? property.Signature.ReturnType
                : imports.Module.IntPtr());
            getter.CilMethodBody.LocalVariables.Add(local0);

            var localIsPointer = false;
            if (field.Signature!.FieldType.IsValueType && !property.Signature.ReturnType.IsValueType)
            {
                var pointerStore = imports.Il2CppClassPointerStore.MakeGenericInstanceType(property.Signature.ReturnType).ToTypeDefOrRef();
                var pointerStoreType = property.DeclaringType.Module!.DefaultImporter.ImportType(pointerStore);
                getterBody.Add(OpCodes.Ldsfld,
                    new MemberReference(pointerStoreType, "NativeClassPtr", new FieldSignature(imports.Module.IntPtr())));
                getterBody.Add(OpCodes.Ldc_I4, 0);
                getterBody.Add(OpCodes.Conv_U);
                getterBody.Add(OpCodes.Call, imports.IL2CPP_il2cpp_class_value_size.Value);
                getterBody.Add(OpCodes.Conv_U);
                getterBody.Add(OpCodes.Localloc);
                getterBody.Add(OpCodes.Stloc, local0);
                localIsPointer = true;
            }

            getterBody.Add(OpCodes.Ldsfld, fieldContext.PointerField);
            if (localIsPointer)
                getterBody.Add(OpCodes.Ldloc, local0);
            else
                getterBody.Add(OpCodes.Ldloca_S, local0);
            getterBody.Add(OpCodes.Conv_U);
            getterBody.Add(OpCodes.Call, imports.IL2CPP_il2cpp_field_static_get_value.Value);

            if (property.Signature.ReturnType.IsValueType)
            {
                getterBody.Add(OpCodes.Ldloc, local0);
                getterBody.Add(OpCodes.Ret);

                property.GetMethod = getter;
                return;
            }
        }
        else
        {
            local0 = new CilLocalVariable(imports.Module.IntPtr());
            getter.CilMethodBody.LocalVariables.Add(local0);

            getterBody.EmitObjectToPointer(fieldContext.DeclaringType.OriginalType.ToTypeSignature(), fieldContext.DeclaringType.NewType.ToTypeSignature(),
                fieldContext.DeclaringType, 0, false, false, false, false, out _);
            getterBody.Add(OpCodes.Ldsfld, fieldContext.PointerField);
            getterBody.Add(OpCodes.Call, imports.IL2CPP_il2cpp_field_get_offset.Value);
            getterBody.Add(OpCodes.Add);

            getterBody.Add(OpCodes.Stloc_0);
        }

        getterBody.EmitPointerToObject(fieldContext.OriginalField.Signature!.FieldType, property.Signature.ReturnType,
            fieldContext.DeclaringType, local0, !field.IsStatic, false);

        if (property.Signature.ReturnType.IsPointerLike())
            getterBody.Add(OpCodes.Ldind_I);

        getterBody.Add(OpCodes.Ret);

        property.GetMethod = getter;
    }

    public static void MakeSetter(FieldDefinition field, FieldRewriteContext fieldContext, PropertyDefinition property,
        RuntimeAssemblyReferences imports)
    {
        var attributes = Field2MethodAttrs(field.Attributes) | MethodAttributes.SpecialName | MethodAttributes.HideBySig;
        var setter = new MethodDefinition("set_" + property.Name,
            attributes,
            MethodSignatureCreator.CreateMethodSignature(attributes, imports.Module.Void(), 0, property.Signature!.ReturnType));
        property.DeclaringType!.Methods.Add(setter);
        setter.CilMethodBody = new(setter);
        var setterBody = setter.CilMethodBody.Instructions;

        if (field.IsStatic)
        {
            setterBody.Add(OpCodes.Ldsfld, fieldContext.PointerField);
            setterBody.EmitObjectToPointer(field.Signature!.FieldType, property.Signature.ReturnType, fieldContext.DeclaringType, 0, false,
                true, true, true, out _);
            setterBody.Add(OpCodes.Call, imports.IL2CPP_il2cpp_field_static_set_value.Value);
        }
        else
        {
            setterBody.EmitObjectToPointer(fieldContext.DeclaringType.OriginalType.ToTypeSignature(), fieldContext.DeclaringType.NewType.ToTypeSignature(),
                fieldContext.DeclaringType, 0, false, false, false, false, out _);
            setterBody.Add(OpCodes.Dup);
            setterBody.Add(OpCodes.Ldsfld, fieldContext.PointerField);
            setterBody.Add(OpCodes.Call, imports.IL2CPP_il2cpp_field_get_offset.Value);
            setterBody.Add(OpCodes.Add);
            setterBody.EmitObjectStore(field.Signature!.FieldType, property.Signature.ReturnType, fieldContext.DeclaringType, 1);
        }

        setterBody.Add(OpCodes.Ret);

        property.SetMethod = setter;
    }

    private static MethodAttributes Field2MethodAttrs(FieldAttributes fieldAttributes)
    {
        if ((fieldAttributes & FieldAttributes.Static) != 0)
            return MethodAttributes.Public | MethodAttributes.Static;
        return MethodAttributes.Public;
    }
}
