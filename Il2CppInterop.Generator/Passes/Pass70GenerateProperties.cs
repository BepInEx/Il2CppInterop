using System.Reflection;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;
using Il2CppInterop.Generator.Utils;

namespace Il2CppInterop.Generator.Passes;

public static class Pass70GenerateProperties
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                var type = typeContext.OriginalType;
                var propertyCountsByName = new Dictionary<string, int>();

                foreach (var oldProperty in type.Properties)
                {
                    if ((oldProperty.GetMethod?.HasOverrides() ?? false) || (oldProperty.SetMethod?.HasOverrides() ?? false)) continue;

                    var unmangledPropertyName = UnmanglePropertyName(assemblyContext, oldProperty, typeContext.NewType,
                        propertyCountsByName);

                    var property = new PropertyDefinition(unmangledPropertyName, oldProperty.Attributes,
                        new PropertySignature(CallingConventionAttributes.Default, assemblyContext.RewriteTypeRef(oldProperty.Signature!.ReturnType), []));
                    foreach (var oldParameter in oldProperty.Signature.ParameterTypes)
                        property.Signature.ParameterTypes.Add(assemblyContext.RewriteTypeRef(oldParameter));

                    typeContext.NewType.Properties.Add(property);

                    property.SetSemanticMethods(
                        oldProperty.GetMethod is null ? null : typeContext.GetMethodByOldMethod(oldProperty.GetMethod).NewMethod,
                        oldProperty.SetMethod is null ? null : typeContext.GetMethodByOldMethod(oldProperty.SetMethod).NewMethod);
                }

                string? defaultMemberName = null;
                var defaultMemberAttributeAttribute = type.CustomAttributes.FirstOrDefault(it =>
                    it.AttributeType()?.Name == "AttributeAttribute" && it.Signature.NamedArguments.Any(it =>
                        it.MemberName == "Name" && (string?)it.Argument.Element == nameof(DefaultMemberAttribute)));
                if (defaultMemberAttributeAttribute != null)
                {
                    defaultMemberName = "Item";
                }
                else
                {
                    var realDefaultMemberAttribute =
                        type.CustomAttributes.FirstOrDefault(it => it.AttributeType()?.Name == nameof(DefaultMemberAttribute));
                    if (realDefaultMemberAttribute != null)
                        defaultMemberName = realDefaultMemberAttribute.Signature?.FixedArguments[0].Element?.ToString() ?? "Item";
                }

                if (defaultMemberName != null)
                    typeContext.NewType.CustomAttributes.Add(new CustomAttribute(
                        CecilAdapter.CreateInstanceMethodReference(".ctor", assemblyContext.Imports.Module.Void(),
                            assemblyContext.Imports.Module.DefaultMemberAttribute().ToTypeDefOrRef(), assemblyContext.Imports.Module.String()),
                        new CustomAttributeSignature(new CustomAttributeArgument(assemblyContext.Imports.Module.String(), defaultMemberName))));
            }
    }

    private static string UnmanglePropertyName(AssemblyRewriteContext assemblyContext, PropertyDefinition prop,
        ITypeDefOrRef declaringType, Dictionary<string, int> countsByBaseName)
    {
        if (assemblyContext.GlobalContext.Options.PassthroughNames ||
            !prop.Name.IsObfuscated(assemblyContext.GlobalContext.Options)) return prop.Name;

        var baseName = "prop_" + assemblyContext.RewriteTypeRef(prop.Signature.ReturnType).GetUnmangledName();

        countsByBaseName.TryGetValue(baseName, out var index);
        countsByBaseName[baseName] = index + 1;

        var unmanglePropertyName = baseName + "_" + index;

        if (assemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                declaringType.GetNamespacePrefix() + "." + declaringType.Name + "::" + unmanglePropertyName, out var newNameByType))
        {
            unmanglePropertyName = newNameByType;
        }
        else if (assemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
                       declaringType.GetNamespacePrefix() + "::" + unmanglePropertyName, out var newName))
        {
            unmanglePropertyName = newName;
        }


        return unmanglePropertyName;
    }
}
