using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass79UnstripTypes
{
    public static void DoPass(RewriteGlobalContext context)
    {
        var typesUnstripped = 0;

        foreach (var unityAssembly in context.UnityAssemblies.Assemblies)
        {
            var processedAssembly = context.TryGetAssemblyByName(unityAssembly.Name);
            if (processedAssembly == null)
            {
                var newAssembly = new AssemblyDefinition(unityAssembly.Name, unityAssembly.Version);
                newAssembly.Modules.Add(new ModuleDefinition(unityAssembly.ManifestModule!.Name));
                var newContext = new AssemblyRewriteContext(context, unityAssembly,
                    newAssembly);
                context.AddAssemblyContext(unityAssembly.Name!, newContext);
                processedAssembly = newContext;
            }

            var imports = processedAssembly.Imports;

            foreach (var unityType in unityAssembly.ManifestModule!.TopLevelTypes)
                ProcessType(processedAssembly, unityType, null, imports, ref typesUnstripped);
        }

        Logger.Instance.LogTrace("Unstripped {UnstrippedTypeCount} types", typesUnstripped);
    }

    private static void ProcessType(AssemblyRewriteContext processedAssembly, TypeDefinition unityType,
        TypeDefinition? enclosingNewType, RuntimeAssemblyReferences imports, ref int typesUnstripped)
    {
        if (unityType.Name == "<Module>")
            return;

        // Don't unstrip delegates, the il2cpp runtime methods are stripped and we cannot recover them
        if (unityType.BaseType != null && unityType.BaseType.FullName == "System.MulticastDelegate")
            return;
        var newModule = processedAssembly.NewAssembly.ManifestModule!;
        var processedType = enclosingNewType == null
            ? processedAssembly.TryGetTypeByName(unityType.FullName)?.NewType
            : enclosingNewType.NestedTypes.SingleOrDefault(it => it.Name == unityType.Name);
        if (unityType.IsEnum)
        {
            if (processedType != null) return;

            typesUnstripped++;
            var clonedType = CloneEnum(unityType, imports);
            if (enclosingNewType == null)
            {
                newModule.TopLevelTypes.Add(clonedType);
            }
            else
            {
                enclosingNewType.NestedTypes.Add(clonedType);
            }

            processedAssembly.RegisterTypeRewrite(new TypeRewriteContext(processedAssembly, null, clonedType));

            return;
        }

        if (processedType == null && !unityType.IsEnum && !HasNonBlittableFields(unityType) &&
            !unityType.HasGenericParameters()) // restore all types even if it would be not entirely correct
        {
            typesUnstripped++;
            var clonedType = new TypeDefinition(unityType.Namespace, unityType.Name, ForcePublic(unityType.Attributes), unityType.BaseType == null ? null : newModule.DefaultImporter.ImportType(unityType.BaseType));
            if (enclosingNewType == null)
            {
                newModule.TopLevelTypes.Add(clonedType);
            }
            else
            {
                enclosingNewType.NestedTypes.Add(clonedType);
            }

            // Unity assemblies sometimes have struct layouts on classes.
            // This gets overlooked on mono but not on coreclr.
            if (!clonedType.IsValueType && (clonedType.IsExplicitLayout || clonedType.IsSequentialLayout))
            {
                clonedType.IsExplicitLayout = clonedType.IsSequentialLayout = false;
                clonedType.IsAutoLayout = true;
            }

            processedAssembly.RegisterTypeRewrite(new TypeRewriteContext(processedAssembly, null, clonedType));
            processedType = clonedType;
        }

        foreach (var nestedUnityType in unityType.NestedTypes)
            ProcessType(processedAssembly, nestedUnityType, processedType, imports, ref typesUnstripped);
    }

    private static TypeDefinition CloneEnum(TypeDefinition sourceEnum, RuntimeAssemblyReferences imports)
    {
        var newType = new TypeDefinition(sourceEnum.Namespace, sourceEnum.Name, ForcePublic(sourceEnum.Attributes),
            imports.Module.Enum().ToTypeDefOrRef());
        foreach (var sourceEnumField in sourceEnum.Fields)
        {
            TypeSignature fieldType = sourceEnumField.Name == "value__"
                ? imports.Module.ImportCorlibReference(sourceEnumField.Signature!.FieldType.FullName)
                : newType.ToTypeSignature();
            var newField = new FieldDefinition(sourceEnumField.Name, sourceEnumField.Attributes, new FieldSignature(fieldType));
            newField.Constant = sourceEnumField.Constant;
            newType.Fields.Add(newField);
        }

        return newType;
    }

    private static bool HasNonBlittableFields(TypeDefinition type)
    {
        if (!type.IsValueType) return false;

        var typeSignature = type.ToTypeSignature();
        foreach (var fieldDefinition in type.Fields)
        {
            if (fieldDefinition.IsStatic || SignatureComparer.Default.Equals(fieldDefinition.Signature?.FieldType, typeSignature))
                continue;

            if (!fieldDefinition.Signature!.FieldType.IsValueType)
                return true;

            if (fieldDefinition.Signature.FieldType.Namespace?.StartsWith("System") ?? false &&
                HasNonBlittableFields(fieldDefinition.Signature.FieldType.Resolve()))
                return true;
        }

        return false;
    }

    private static TypeAttributes ForcePublic(TypeAttributes typeAttributes)
    {
        var visibility = typeAttributes & TypeAttributes.VisibilityMask;
        if (visibility == 0 || visibility == TypeAttributes.Public)
            return typeAttributes | TypeAttributes.Public;

        return (typeAttributes & ~TypeAttributes.VisibilityMask) | TypeAttributes.NestedPublic;
    }
}
