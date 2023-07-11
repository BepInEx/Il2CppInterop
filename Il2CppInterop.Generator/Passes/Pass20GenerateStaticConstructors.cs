using System;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Passes;

public static class Pass20GenerateStaticConstructors
{
    private static int ourTokenlessMethods;

    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                GenerateStaticProxy(assemblyContext, typeContext);

        Logger.Instance.LogTrace("Tokenless method count: {TokenlessMethodCount}", ourTokenlessMethods);
    }

    private static void GenerateStaticProxy(AssemblyRewriteContext assemblyContext, TypeRewriteContext typeContext)
    {
        var oldType = typeContext.OriginalType;
        var newType = typeContext.NewType;
        if (newType.IsEnum) return;

        var staticCtorMethod = new MethodDefinition(".cctor",
            MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.SpecialName |
            MethodAttributes.HideBySig | MethodAttributes.RTSpecialName, assemblyContext.Imports.Module.Void());
        newType.Methods.Add(staticCtorMethod);

        var ctorBuilder = staticCtorMethod.Body.GetILProcessor();

        if (newType.IsNested && oldType.IsNested)
        {
            ctorBuilder.Emit(OpCodes.Ldsfld,
                assemblyContext.GlobalContext.GetNewTypeForOriginal(oldType.DeclaringType).ClassPointerFieldRef);
            ctorBuilder.Emit(OpCodes.Ldstr, oldType.Name);
            ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppNestedType.Value);
        }
        else
        {
            ctorBuilder.Emit(OpCodes.Ldstr, oldType.Module.Name);
            ctorBuilder.Emit(OpCodes.Ldstr, oldType.Namespace);
            ctorBuilder.Emit(OpCodes.Ldstr, oldType.Name);
            ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppClass.Value);
        }

        if (oldType.HasGenericParameters)
        {
            var il2CppTypeTypeRewriteContext = assemblyContext.GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Type");
            var il2CppSystemTypeRef = newType.Module.ImportReference(il2CppTypeTypeRewriteContext.NewType);

            var il2CppTypeHandleTypeRewriteContext = assemblyContext.GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.RuntimeTypeHandle");
            var il2CppSystemTypeHandleRef = newType.Module.ImportReference(il2CppTypeHandleTypeRewriteContext.NewType);

            ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_get_type.Value);
            ctorBuilder.Emit(OpCodes.Call,
                new MethodReference("internal_from_handle", il2CppSystemTypeRef, il2CppSystemTypeRef)
                { Parameters = { new ParameterDefinition(assemblyContext.Imports.Module.IntPtr()) } });

            ctorBuilder.EmitLdcI4(oldType.GenericParameters.Count);

            ctorBuilder.Emit(OpCodes.Newarr, il2CppSystemTypeRef);

            for (var i = 0; i < oldType.GenericParameters.Count; i++)
            {
                ctorBuilder.Emit(OpCodes.Dup);
                ctorBuilder.EmitLdcI4(i);

                var param = oldType.GenericParameters[i];
                var storeRef = new GenericInstanceType(assemblyContext.Imports.Il2CppClassPointerStore)
                { GenericArguments = { param } };
                var fieldRef = new FieldReference("NativeClassPtr", assemblyContext.Imports.Module.IntPtr(), storeRef);
                ctorBuilder.Emit(OpCodes.Ldsfld, fieldRef);

                ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_get_type.Value);

                ctorBuilder.Emit(OpCodes.Call,
                    new MethodReference("internal_from_handle", il2CppSystemTypeRef, il2CppSystemTypeRef)
                    { Parameters = { new ParameterDefinition(assemblyContext.Imports.Module.IntPtr()) } });
                ctorBuilder.Emit(OpCodes.Stelem_Ref);
            }

            var il2CppTypeArray = new GenericInstanceType(assemblyContext.Imports.Il2CppReferenceArray)
            { GenericArguments = { il2CppSystemTypeRef } };
            ctorBuilder.Emit(OpCodes.Newobj,
                new MethodReference(".ctor", assemblyContext.Imports.Module.Void(), il2CppTypeArray)
                {
                    HasThis = true,
                    Parameters =
                    {
                        new ParameterDefinition(
                            new ArrayType(assemblyContext.Imports.Il2CppReferenceArray.GenericParameters[0]))
                    }
                });
            ctorBuilder.Emit(OpCodes.Call,
                new MethodReference(nameof(Type.MakeGenericType), il2CppSystemTypeRef, il2CppSystemTypeRef)
                { HasThis = true, Parameters = { new ParameterDefinition(il2CppTypeArray) } });

            ctorBuilder.Emit(OpCodes.Call,
                new MethodReference(typeof(Type).GetProperty(nameof(Type.TypeHandle))!.GetMethod!.Name,
                    il2CppSystemTypeHandleRef, il2CppSystemTypeRef)
                { HasThis = true });
            ctorBuilder.Emit(OpCodes.Ldfld,
                new FieldReference("value", assemblyContext.Imports.Module.IntPtr(), il2CppSystemTypeHandleRef));

            ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_from_type.Value);
        }

        ctorBuilder.Emit(OpCodes.Stsfld, typeContext.ClassPointerFieldRef);

        if (oldType.IsBeforeFieldInit)
        {
            ctorBuilder.Emit(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
            ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_runtime_class_init.Value);
        }

        if (oldType.IsEnum)
        {
            ctorBuilder.Emit(OpCodes.Ret);
            return;
        }

        foreach (var field in typeContext.Fields)
        {
            ctorBuilder.Emit(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
            ctorBuilder.Emit(OpCodes.Ldstr, field.OriginalField.Name);
            ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppField.Value);
            ctorBuilder.Emit(OpCodes.Stsfld, field.PointerField);
        }

        foreach (var method in typeContext.Methods)
        {
            ctorBuilder.Emit(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);

            var token = method.OriginalMethod.ExtractToken();
            if (token == 0)
            {
                ourTokenlessMethods++;

                ctorBuilder.Emit(
                    method.OriginalMethod.GenericParameters.Count > 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                ctorBuilder.Emit(OpCodes.Ldstr, method.OriginalMethod.Name);
                ctorBuilder.EmitLoadTypeNameString(assemblyContext.Imports, method.OriginalMethod,
                    method.OriginalMethod.ReturnType, method.NewMethod.ReturnType);
                ctorBuilder.Emit(OpCodes.Ldc_I4, method.OriginalMethod.Parameters.Count);
                ctorBuilder.Emit(OpCodes.Newarr, assemblyContext.Imports.Module.String());

                for (var i = 0; i < method.OriginalMethod.Parameters.Count; i++)
                {
                    ctorBuilder.Emit(OpCodes.Dup);
                    ctorBuilder.EmitLdcI4(i);
                    ctorBuilder.EmitLoadTypeNameString(assemblyContext.Imports, method.OriginalMethod,
                        method.OriginalMethod.Parameters[i].ParameterType,
                        method.NewMethod.Parameters[i].ParameterType);
                    ctorBuilder.Emit(OpCodes.Stelem_Ref);
                }

                ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppMethod.Value);
            }
            else
            {
                ctorBuilder.EmitLdcI4((int)token);
                ctorBuilder.Emit(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppMethodByToken.Value);
            }

            ctorBuilder.Emit(OpCodes.Stsfld, method.NonGenericMethodInfoPointerField);
        }

        ctorBuilder.Emit(OpCodes.Ret);
    }

    private static void EmitLoadTypeNameString(this ILProcessor ctorBuilder, RuntimeAssemblyReferences imports,
        MethodDefinition originalMethod, TypeReference originalTypeReference, TypeReference newTypeReference)
    {
        if (originalMethod.HasGenericParameters || originalTypeReference.FullName == "System.Void")
        {
            ctorBuilder.Emit(OpCodes.Ldstr, originalTypeReference.FullName);
        }
        else
        {
            ctorBuilder.Emit(newTypeReference.IsByReference ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            ctorBuilder.Emit(OpCodes.Call,
                imports.Module.ImportReference(new GenericInstanceMethod(imports.IL2CPP_RenderTypeName.Value)
                {
                    GenericArguments =
                        {newTypeReference.IsByReference ? newTypeReference.GetElementType() : newTypeReference}
                }));
        }
    }
}
