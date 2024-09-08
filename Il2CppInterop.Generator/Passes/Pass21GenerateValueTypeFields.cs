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
                if (typeContext.ComputedTypeSpecifics != TypeRewriteContext.TypeSpecifics.BlittableStruct ||
                    typeContext.OriginalType.IsEnum) continue;

                try
                {
                    var newType = typeContext.NewType;
                    newType.Attributes = (newType.Attributes & ~TypeAttributes.LayoutMask) |
                                         TypeAttributes.ExplicitLayout;

                    ILGeneratorEx.GenerateBoxMethod(assemblyContext.Imports, newType, typeContext.ClassPointerFieldRef,
                        il2CppSystemTypeRef);

                    foreach (var fieldContext in typeContext.Fields)
                    {
                        var field = fieldContext.OriginalField;
                        if (field.IsStatic) continue;

                        var newField = new FieldDefinition(fieldContext.UnmangledName, field.Attributes.ForcePublic(),
                            !field.Signature!.FieldType.IsValueType
                                ? assemblyContext.Imports.Module.IntPtr()
                                : assemblyContext.RewriteTypeRef(field.Signature.FieldType));

                        newField.FieldOffset = field.ExtractFieldOffset();

                        // Special case: bools in Il2Cpp are bytes
                        if (newField.Signature!.FieldType.FullName == "System.Boolean")
                            newField.MarshalDescriptor = new SimpleMarshalDescriptor(NativeType.U1);

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
