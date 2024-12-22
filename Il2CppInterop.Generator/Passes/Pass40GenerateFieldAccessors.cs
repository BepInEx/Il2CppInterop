using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass40GenerateFieldAccessors
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                foreach (var fieldContext in typeContext.Fields)
                {
                    if (typeContext.ComputedTypeSpecifics == TypeRewriteContext.TypeSpecifics.BlittableStruct &&
                        !fieldContext.OriginalField.IsStatic) continue;

                    var field = fieldContext.OriginalField;
                    var unmangleFieldName = fieldContext.UnmangledName;

                    var propertyType = assemblyContext.RewriteTypeRef(fieldContext.OriginalField.Signature!.FieldType);
                    var signature = field.IsStatic
                        ? PropertySignature.CreateStatic(propertyType)
                        : PropertySignature.CreateInstance(propertyType);
                    var property = new PropertyDefinition(unmangleFieldName, PropertyAttributes.None, signature);
                    typeContext.NewType.Properties.Add(property);

                    FieldAccessorGenerator.MakeGetter(field, fieldContext, property, assemblyContext.Imports);
                    FieldAccessorGenerator.MakeSetter(field, fieldContext, property, assemblyContext.Imports);
                }
            }
        }
    }
}
