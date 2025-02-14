using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass21GenerateValueTypeFields
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            var il2CppTypeTypeRewriteContext = assemblyContext.GlobalContext.GetAssemblyByName("mscorlib")
                .GetTypeByName("System.Object");
            var il2CppSystemTypeRef =
                assemblyContext.NewAssembly.ManifestModule!.DefaultImporter.ImportType(il2CppTypeTypeRewriteContext.NewType).ToTypeSignature();

            foreach (var typeContext in assemblyContext.Types)
            {
                if (!typeContext.ComputedTypeSpecifics.IsBlittable() ||
                    typeContext.OriginalType.IsEnum) continue;

                try
                {
                    var newType = typeContext.NewType;

                    if (!typeContext.OriginalType.HasGenericParameters())
                        newType.Attributes = (newType.Attributes & ~TypeAttributes.LayoutMask) |
                                             TypeAttributes.ExplicitLayout;
                    else
                        newType.IsSequentialLayout = true;

                    if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
                    {
                        var boxedType = typeContext.BoxedTypeContext.NewType;
                        var typeRef = assemblyContext.Imports.Module.DefaultImporter.ImportType(boxedType);
                        var genericBoxedType = new GenericInstanceTypeSignature(typeRef, typeRef.IsValueType);
                        foreach (GenericParameter parameter in newType.GenericParameters)
                            genericBoxedType.TypeArguments.Add(parameter.ToTypeSignature());
                        ILGeneratorEx.GenerateBoxMethod(assemblyContext.Imports, newType, typeContext.ClassPointerFieldRef,
                            genericBoxedType);
                    }
                    else
                    {
                        ILGeneratorEx.GenerateBoxMethod(assemblyContext.Imports, newType, typeContext.ClassPointerFieldRef,
                            il2CppSystemTypeRef);
                    }

                    foreach (var fieldContext in typeContext.Fields)
                    {
                        var field = fieldContext.OriginalField;
                        if (field.IsStatic) continue;

                        TypeSignature rewriteTypeRef;
                        if (!field.Signature!.FieldType.IsValueType && field.Signature.FieldType is not PointerTypeSignature and not GenericParameterSignature)
                            rewriteTypeRef = assemblyContext.Imports.Module.IntPtr();
                        else
                            rewriteTypeRef = assemblyContext.RewriteTypeRef(field.Signature.FieldType, newType.GetGenericParameterContext());

                        var newField = new FieldDefinition(fieldContext.UnmangledName, field.Attributes.ForcePublic(), rewriteTypeRef);

                        if (!typeContext.OriginalType.HasGenericParameters())
                            newField.FieldOffset = field.ExtractFieldOffset();


                        // Special case: bools in Il2Cpp are bytes
                        if (newField.Signature!.FieldType.FullName == "System.Boolean")
                        {
                            if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
                            {
                                newField.Signature.FieldType = assemblyContext.Imports.NativeBoolean;
                            }
                            else
                            {
                                newField.MarshalDescriptor = new SimpleMarshalDescriptor(NativeType.U1);
                            }
                        }

                        newType.Fields.Add(newField);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception(
                        $"Failed to generate value type fields for type {typeContext.OriginalType.FullName} in assembly {typeContext.AssemblyContext.OriginalAssembly.Name}",
                        ex);
                }
            }
        }
    }
}
