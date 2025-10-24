using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;

namespace Il2CppInterop.Generator;

public class IndexerAttributeInjectionProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "indexer_attribute_injection";
    public override string Name => "Indexer Attribute Injection";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var defaultMemberAttribute = appContext.Mscorlib.GetTypeByFullNameOrThrow("System.Reflection.DefaultMemberAttribute");
        var defaultMemberAttributeConstructor = defaultMemberAttribute.Methods.First(m => m.IsInstanceConstructor && m.Parameters.Count == 1 && m.Parameters[0].ParameterType == appContext.SystemTypes.SystemStringType);

        foreach (var type in appContext.AllTypes)
        {
            if (!type.Properties.Any(IsIndexerProperty))
                continue;

            type.CustomAttributes ??= new();

            var customAttribute = new AnalyzedCustomAttribute(defaultMemberAttributeConstructor);
            customAttribute.ConstructorParameters.Add(new CustomAttributePrimitiveParameter("Item", customAttribute, CustomAttributeParameterKind.ConstructorParam, 0));
            type.CustomAttributes.Add(customAttribute);
        }
    }

    private static bool IsIndexerProperty(PropertyAnalysisContext property)
    {
        return property.Name == "Item" && property is { Getter.Parameters.Count: > 0 } or { Setter.Parameters.Count: > 1 };
    }
}
