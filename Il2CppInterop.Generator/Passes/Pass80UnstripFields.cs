using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass80UnstripFields
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var fieldsUnstripped = 0;
        var fieldsIgnored = 0;

        foreach (var unityAssembly in context.UnityAssemblies.Assemblies)
        {
            var processedAssembly = context.TryGetAssemblyByName(unityAssembly.Name);
            if (processedAssembly == null) continue;
            var imports = processedAssembly.Imports;

            foreach (var unityType in unityAssembly.ManifestModule!.TopLevelTypes)
            {
                var processedType = processedAssembly.TryGetTypeByName(unityType.FullName);
                if (processedType == null) continue;

                if (!unityType.IsValueType || unityType.IsEnum)
                    continue;

                foreach (var unityField in unityType.Fields)
                {
                    if (unityField.IsStatic && !unityField.HasConstant()) continue;
                    if (processedType.NewType.IsExplicitLayout && !unityField.IsStatic) continue;

                    var processedField = processedType.TryGetFieldByUnityAssemblyField(unityField);
                    if (processedField != null) continue;

                    var fieldType =
                        Pass80UnstripMethods.ResolveTypeInNewAssemblies(context, unityField.Signature!.FieldType, imports);
                    if (fieldType == null)
                    {
                        Logger.Instance.LogTrace("Field {UnityField} on type {UnityType} has unsupported type {UnityFieldType}, the type will be unusable", unityField.ToString(), unityType.FullName, unityField.Signature.FieldType.ToString());
                        fieldsIgnored++;
                        continue;
                    }

                    var newField = new FieldDefinition(unityField.Name!,
                        (unityField.Attributes & ~FieldAttributes.FieldAccessMask) | FieldAttributes.Public, fieldType);

                    if (unityField.HasConstant()) newField.Constant = unityField.Constant;

                    processedType.NewType.Fields.Add(newField);

                    fieldsUnstripped++;
                }
            }
        }

        Logger.Instance.LogInformation("Restored {FieldsUnstripped} fields", fieldsUnstripped);
        Logger.Instance.LogInformation("Failed to restore {FieldsIgnored} fields", fieldsIgnored);
    }
}
