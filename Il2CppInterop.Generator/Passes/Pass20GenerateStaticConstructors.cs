using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

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

        var staticCtorMethod = newType.GetOrCreateStaticConstructor();

        var ctorBuilder = staticCtorMethod.CilMethodBody!.Instructions;
        ctorBuilder.Clear();

        if (newType.IsNested)
        {
            ctorBuilder.Add(OpCodes.Ldsfld,
                assemblyContext.GlobalContext.GetNewTypeForOriginal(oldType.DeclaringType!).ClassPointerFieldRef);
            ctorBuilder.Add(OpCodes.Ldstr, oldType.Name ?? "");
            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppNestedType.Value);
        }
        else
        {
            ctorBuilder.Add(OpCodes.Ldstr, oldType.Module?.Name ?? "");
            ctorBuilder.Add(OpCodes.Ldstr, oldType.Namespace ?? "");
            ctorBuilder.Add(OpCodes.Ldstr, oldType.Name ?? "");
            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppClass.Value);
        }

        if (oldType.HasGenericParameters())
        {
            var il2CppTypeTypeRewriteContext = assemblyContext.GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Type");
            var il2CppSystemTypeRef = newType.Module!.DefaultImporter.ImportType(il2CppTypeTypeRewriteContext.NewType);

            var il2CppTypeHandleTypeRewriteContext = assemblyContext.GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.RuntimeTypeHandle");
            var il2CppSystemTypeHandleRef = newType.Module.DefaultImporter.ImportType(il2CppTypeHandleTypeRewriteContext.NewType);

            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_get_type.Value);
            ctorBuilder.Add(OpCodes.Call,
                new MemberReference(il2CppSystemTypeRef, "internal_from_handle", MethodSignature.CreateStatic(il2CppSystemTypeRef.ToTypeSignature(), assemblyContext.Imports.Module.IntPtr())));

            ctorBuilder.Add(OpCodes.Ldc_I4, oldType.GenericParameters.Count);

            ctorBuilder.Add(OpCodes.Newarr, il2CppSystemTypeRef);

            for (var i = 0; i < oldType.GenericParameters.Count; i++)
            {
                ctorBuilder.Add(OpCodes.Dup);
                ctorBuilder.Add(OpCodes.Ldc_I4, i);

                var param = oldType.GenericParameters[i];
                var storeRef = assemblyContext.Imports.Il2CppClassPointerStore
                    .MakeGenericInstanceType(new GenericParameterSignature(GenericParameterType.Type, param.Number));
                var fieldRef = new MemberReference(storeRef.ToTypeDefOrRef(), "NativeClassPtr", new FieldSignature(assemblyContext.Imports.Module.IntPtr()));
                ctorBuilder.Add(OpCodes.Ldsfld, fieldRef);

                ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_get_type.Value);

                ctorBuilder.Add(OpCodes.Call,
                    new MemberReference(il2CppSystemTypeRef, "internal_from_handle", MethodSignature.CreateStatic(il2CppSystemTypeRef.ToTypeSignature(), assemblyContext.Imports.Module.IntPtr())));
                ctorBuilder.Add(OpCodes.Stelem_Ref);
            }

            var il2CppTypeArray = assemblyContext.Imports.Il2CppReferenceArray.MakeGenericInstanceType(il2CppSystemTypeRef.ToTypeSignature());
            ctorBuilder.Add(OpCodes.Newobj,
                new MemberReference(il2CppTypeArray.ToTypeDefOrRef(), ".ctor", MethodSignature.CreateInstance(assemblyContext.Imports.Module.Void(), new GenericParameterSignature(GenericParameterType.Type, 0).MakeSzArrayType())));
            ctorBuilder.Add(OpCodes.Call,
                ReferenceCreator.CreateInstanceMethodReference(nameof(Type.MakeGenericType), il2CppSystemTypeRef.ToTypeSignature(), il2CppSystemTypeRef, il2CppTypeArray));

            ctorBuilder.Add(OpCodes.Call,
                ReferenceCreator.CreateInstanceMethodReference(typeof(Type).GetProperty(nameof(Type.TypeHandle))!.GetMethod!.Name,
                    il2CppSystemTypeHandleRef.ToTypeSignature(), il2CppSystemTypeRef));
            ctorBuilder.Add(OpCodes.Ldfld,
                ReferenceCreator.CreateFieldReference("value", assemblyContext.Imports.Module.IntPtr(), il2CppSystemTypeHandleRef));

            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_from_type.Value);
        }

        ctorBuilder.Add(OpCodes.Stsfld, typeContext.ClassPointerFieldRef);

        if (oldType.IsBeforeFieldInit)
        {
            ctorBuilder.Add(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_runtime_class_init.Value);
        }

        if (oldType.IsEnum)
        {
            ctorBuilder.Add(OpCodes.Ret);
            return;
        }

        foreach (var field in typeContext.Fields)
        {
            ctorBuilder.Add(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
            ctorBuilder.Add(OpCodes.Ldstr, field.OriginalField.Name!);
            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppField.Value);
            ctorBuilder.Add(OpCodes.Stsfld, field.PointerField);
        }

        foreach (var method in typeContext.Methods)
        {
            ctorBuilder.Add(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);

            var token = method.OriginalMethod.ExtractToken();
            if (token == 0)
            {
                ourTokenlessMethods++;

                ctorBuilder.Add(
                    method.OriginalMethod.GenericParameters.Count > 0 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                ctorBuilder.Add(OpCodes.Ldstr, method.OriginalMethod.Name!);
                ctorBuilder.EmitLoadTypeNameString(assemblyContext.Imports, method.OriginalMethod,
                    method.OriginalMethod.Signature!.ReturnType, method.NewMethod.Signature!.ReturnType);
                ctorBuilder.Add(OpCodes.Ldc_I4, method.OriginalMethod.Parameters.Count);
                ctorBuilder.Add(OpCodes.Newarr, assemblyContext.Imports.Module.String().ToTypeDefOrRef());

                for (var i = 0; i < method.OriginalMethod.Parameters.Count; i++)
                {
                    ctorBuilder.Add(OpCodes.Dup);
                    ctorBuilder.Add(OpCodes.Ldc_I4, i);
                    ctorBuilder.EmitLoadTypeNameString(assemblyContext.Imports, method.OriginalMethod,
                        method.OriginalMethod.Parameters[i].ParameterType,
                        method.NewMethod.Parameters[i].ParameterType);
                    ctorBuilder.Add(OpCodes.Stelem_Ref);
                }

                ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppMethod.Value);
            }
            else
            {
                ctorBuilder.Add(OpCodes.Ldc_I4, (int)token);
                ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_GetIl2CppMethodByToken.Value);
            }

            ctorBuilder.Add(OpCodes.Stsfld, method.NonGenericMethodInfoPointerField);
        }

        ctorBuilder.Add(OpCodes.Ret);
    }

    private static void EmitLoadTypeNameString(this ILProcessor ctorBuilder, RuntimeAssemblyReferences imports,
        MethodDefinition originalMethod, TypeSignature originalTypeReference, TypeSignature newTypeReference)
    {
        if (originalMethod.HasGenericParameters() || originalTypeReference.FullName == "System.Void")
        {
            ctorBuilder.Add(OpCodes.Ldstr, originalTypeReference.FullName);
        }
        else
        {
            ctorBuilder.Add(newTypeReference is ByReferenceTypeSignature ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
            ctorBuilder.Add(OpCodes.Call,
                imports.Module.DefaultImporter.ImportMethod(
                    imports.IL2CPP_RenderTypeName.Value
                        .MakeGenericInstanceMethod(newTypeReference is ByReferenceTypeSignature ? newTypeReference.GetElementType() : newTypeReference)));
        }
    }
}
