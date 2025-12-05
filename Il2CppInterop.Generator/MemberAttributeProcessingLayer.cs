using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Il2CppInterop.Common.Attributes;

namespace Il2CppInterop.Generator;

public class MemberAttributeProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Member Attribute Processor";

    public override string Id => "member_attribute_processor";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // This layer is responsible for adding Il2CppMethodAttribute and Il2CppPropertyAttribute to methods and properties.
        // Fields are handled in FieldAccessorProcessingLayer
        // Events are handled in EventProcessingLayer

        var il2CppMethodAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppMethodAttribute));
        var il2CppMethodAttributeConstructor = il2CppMethodAttribute.GetMethodByName(".ctor");
        var il2CppMethodAttributeIndex = il2CppMethodAttribute.GetPropertyByName(nameof(Il2CppMethodAttribute.Index));

        var il2CppPropertyAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppPropertyAttribute));
        var il2CppPropertyAttributeConstructor = il2CppPropertyAttribute.GetMethodByName(".ctor");

        var il2CppMemberAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppMemberAttribute));
        var il2CppMemberAttributeName = il2CppMemberAttribute.GetPropertyByName(nameof(Il2CppMemberAttribute.Name));

        foreach (var assembly in appContext.Assemblies)
        {
            if (assembly.IsReferenceAssembly || assembly.IsInjected)
                continue;

            foreach (var type in assembly.Types)
            {
                if (type.IsInjected)
                    continue;

                foreach (var method in type.Methods)
                {
                    if (method.IsInjected)
                        continue;

                    var attribute = new AnalyzedCustomAttribute(il2CppMethodAttributeConstructor);
                    if (method.Name != method.DefaultName)
                    {
                        var parameter = new CustomAttributePrimitiveParameter(method.DefaultName, attribute, CustomAttributeParameterKind.Property, attribute.Properties.Count);
                        attribute.Properties.Add(new CustomAttributeProperty(il2CppMemberAttributeName, parameter));
                    }
                    var index = method.InitializationClassIndex;
                    if (index >= 0)
                    {
                        var parameter = new CustomAttributePrimitiveParameter(index, attribute, CustomAttributeParameterKind.Property, attribute.Properties.Count);
                        attribute.Properties.Add(new CustomAttributeProperty(il2CppMethodAttributeIndex, parameter));
                    }
                    method.CustomAttributes ??= new(1);
                    method.CustomAttributes.Add(attribute);
                }

                foreach (var property in type.Properties)
                {
                    if (property.IsInjected)
                        continue;

                    var attribute = new AnalyzedCustomAttribute(il2CppPropertyAttributeConstructor);
                    if (property.Name != property.DefaultName)
                    {
                        var parameter = new CustomAttributePrimitiveParameter(property.DefaultName, attribute, CustomAttributeParameterKind.Property, 0);
                        attribute.Properties.Add(new CustomAttributeProperty(il2CppMemberAttributeName, parameter));
                    }
                    property.CustomAttributes ??= new(1);
                    property.CustomAttributes.Add(attribute);
                }
            }
        }
    }
}
