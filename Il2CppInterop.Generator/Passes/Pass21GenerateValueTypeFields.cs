using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;

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
                assemblyContext.NewAssembly.MainModule.ImportReference(il2CppTypeTypeRewriteContext.NewType);

            foreach (var typeContext in assemblyContext.Types)
            {
                if (!typeContext.ComputedTypeSpecifics.IsBlittable() ||
                    typeContext.OriginalType.IsEnum) continue;

                try
                {
                    var newType = typeContext.NewType;

                    if (!typeContext.OriginalType.HasGenericParameters)
                        newType.Attributes = (newType.Attributes & ~TypeAttributes.LayoutMask) |
                                             TypeAttributes.ExplicitLayout;
                    else
                        newType.IsSequentialLayout = true;

                    if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
                    {
                        var boxedType = typeContext.BoxedTypeContext.NewType;
                        var genericBoxedType = new GenericInstanceType(assemblyContext.Imports.Module.ImportReference(boxedType));
                        foreach (GenericParameter parameter in newType.GenericParameters)
                            genericBoxedType.GenericArguments.Add(parameter);
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

                        TypeReference rewriteTypeRef;
                        if (!field.FieldType.IsValueType && !field.FieldType.IsPointer && !field.FieldType.IsGenericParameter)
                            rewriteTypeRef = assemblyContext.Imports.Module.IntPtr();
                        else
                            rewriteTypeRef = assemblyContext.RewriteTypeRef(field.FieldType, false);

                        var newField = new FieldDefinition(fieldContext.UnmangledName, field.Attributes.ForcePublic(), rewriteTypeRef);

                        if (!typeContext.OriginalType.HasGenericParameters)
                            newField.Offset = field.ExtractFieldOffset();


                        // Special case: bools in Il2Cpp are bytes
                        if (newField.FieldType.FullName == "System.Boolean")
                        {
                            if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct)
                            {
                                newField.FieldType = assemblyContext.Imports.NativeBoolean;
                            }
                            else
                            {
                                newField.MarshalInfo = new MarshalInfo(NativeType.U1);
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
