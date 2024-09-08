using System.Reflection;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass30GenerateGenericMethodStoreConstructors
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                foreach (var methodContext in typeContext.Methods)
                {
                    var oldMethod = methodContext.OriginalMethod;

                    var storeType = methodContext.GenericInstantiationsStore;
                    if (storeType != null)
                    {
                        var cctor = storeType.GetOrCreateStaticConstructor();

                        var ctorBuilder = cctor.CilMethodBody!.Instructions;
                        ctorBuilder.Clear();

                        var il2CppTypeTypeRewriteContext = assemblyContext.GlobalContext
                            .GetAssemblyByName("mscorlib").GetTypeByName("System.Type");
                        var il2CppSystemTypeRef =
                            assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(il2CppTypeTypeRewriteContext.NewType);

                        var il2CppMethodInfoTypeRewriteContext = assemblyContext.GlobalContext
                            .GetAssemblyByName("mscorlib").GetTypeByName("System.Reflection.MethodInfo");
                        var il2CppSystemReflectionMethodInfoRef =
                            assemblyContext.NewAssembly.ManifestModule.DefaultImporter.ImportType(il2CppMethodInfoTypeRewriteContext.NewType);

                        ctorBuilder.Add(OpCodes.Ldsfld, methodContext.NonGenericMethodInfoPointerField);
                        ctorBuilder.Add(OpCodes.Ldsfld, typeContext.ClassPointerFieldRef);
                        ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_method_get_object.Value);
                        ctorBuilder.Add(OpCodes.Newobj,
                            new MemberReference(il2CppSystemReflectionMethodInfoRef, ".ctor",
                                MethodSignature.CreateInstance(assemblyContext.Imports.Module.Void(), assemblyContext.Imports.Module.IntPtr())));

                        ctorBuilder.Add(OpCodes.Ldc_I4, oldMethod.GenericParameters.Count);

                        ctorBuilder.Add(OpCodes.Newarr, il2CppSystemTypeRef);

                        for (var i = 0; i < oldMethod.GenericParameters.Count; i++)
                        {
                            ctorBuilder.Add(OpCodes.Dup);
                            ctorBuilder.Add(OpCodes.Ldc_I4, i);

                            var param = storeType.GenericParameters[i];
                            var storeRef = assemblyContext.Imports.Il2CppClassPointerStore.MakeGenericInstanceType(new GenericParameterSignature(GenericParameterType.Type, param.Number));
                            var fieldRef = new MemberReference(
                                storeRef.ToTypeDefOrRef(),
                                "NativeClassPtr",
                                new FieldSignature(assemblyContext.Imports.Module.IntPtr()));
                            ctorBuilder.Add(OpCodes.Ldsfld, fieldRef);

                            ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_class_get_type.Value);

                            ctorBuilder.Add(OpCodes.Call,
                                new MemberReference(il2CppSystemTypeRef, "internal_from_handle",
                                MethodSignature.CreateStatic(il2CppSystemTypeRef.ToTypeSignature(), assemblyContext.Imports.Module.IntPtr())));
                            ctorBuilder.Add(OpCodes.Stelem_Ref);
                        }

                        var il2CppTypeArray = assemblyContext.Imports.Il2CppReferenceArray.MakeGenericInstanceType(il2CppSystemTypeRef.ToTypeSignature());
                        ctorBuilder.Add(OpCodes.Newobj,
                            ReferenceCreator.CreateInstanceMethodReference(".ctor", assemblyContext.Imports.Module.Void(), il2CppTypeArray.ToTypeDefOrRef(), new GenericParameterSignature(GenericParameterType.Type, 0).MakeSzArrayType()));
                        ctorBuilder.Add(OpCodes.Call,
                            ReferenceCreator.CreateInstanceMethodReference(nameof(MethodInfo.MakeGenericMethod), il2CppSystemReflectionMethodInfoRef.ToTypeSignature(),
                                    il2CppSystemReflectionMethodInfoRef, il2CppTypeArray));
                        ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_Il2CppObjectBaseToPtrNotNull.Value);

                        ctorBuilder.Add(OpCodes.Call, assemblyContext.Imports.IL2CPP_il2cpp_method_get_from_reflection.Value);
                        ctorBuilder.Add(OpCodes.Stsfld,
                            ReferenceCreator.CreateFieldReference("Pointer", assemblyContext.Imports.Module.IntPtr(),
                                methodContext.GenericInstantiationsStoreSelfSubstRef));

                        ctorBuilder.Add(OpCodes.Ret);
                    }
                }
            }
        }
    }
}
