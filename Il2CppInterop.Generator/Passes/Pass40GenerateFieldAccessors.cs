using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

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

                    // Skip the accessor when the field's type cannot be resolved
                    // from the input assemblies. Substituting a placeholder type
                    // would risk a wrong-sized accessor reading the wrong memory,
                    // so leaving the field inaccessible is the safe outcome.
                    var propertyType =
                        assemblyContext.TryRewriteTypeRef(fieldContext.OriginalField.Signature!.FieldType);

                    if (propertyType == null)
                    {
                        Logger.Instance.LogWarning(
                            "Skipped accessor for {TypeName}.{FieldName}: its type {FieldType} could not be resolved from the input assemblies, so the field will be inaccessible",
                            typeContext.OriginalType.FullName, field.Name?.ToString(),
                            fieldContext.OriginalField.Signature!.FieldType.FullName);

                        continue;
                    }

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
