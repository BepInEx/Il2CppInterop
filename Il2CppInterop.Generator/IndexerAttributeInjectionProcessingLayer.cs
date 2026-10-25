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

        HashSet<string> indexerNames = [];
        foreach (var type in appContext.AllTypes)
        {
            indexerNames.Clear();
            indexerNames.AddRange(type.Properties.Where(IsIndexerProperty).Select(p => p.Name));
            if (indexerNames.Count != 1)
                continue;

            type.CustomAttributes ??= new(1);

            var customAttribute = new AnalyzedCustomAttribute(defaultMemberAttributeConstructor);
            customAttribute.ConstructorParameters.Add(new CustomAttributePrimitiveParameter(indexerNames.First(), customAttribute, CustomAttributeParameterKind.ConstructorParam, 0));
            type.CustomAttributes.Add(customAttribute);
        }
    }

    private static bool IsIndexerProperty(PropertyAnalysisContext property)
    {
        if (property is { Getter.Parameters.Count: > 0, Getter.Overrides.Count: 0 })
        {
            return property.Setter is null || property.Setter.Parameters.Count == property.Getter.Parameters.Count + 1;
        }
        else
        {
            return property is { Setter.Parameters.Count: > 1, Setter.Overrides.Count: 0 };
        }
    }
}
