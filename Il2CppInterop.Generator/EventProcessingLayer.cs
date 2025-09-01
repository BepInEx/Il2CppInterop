using System.Reflection;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;
using Il2CppInterop.Runtime.Attributes;

namespace Il2CppInterop.Generator;

public class EventProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Event Processor";

    public override string Id => "event_processor";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        // C# requires events to have a delegate type and will not allow event syntax without it.
        // https://github.com/ds5678/Il2CppEventTest
        // We remove the event definitions and add attributes to the add/remove/invoke methods instead.

        var il2CppEventAttribute = appContext.ResolveTypeOrThrow(typeof(Il2CppEventAttribute));
        var il2CppEventAttributeConstructor = il2CppEventAttribute.GetMethodByName(".ctor");

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

                for (var i = 0; i < type.Events.Count; i++)
                {
                    var @event = type.Events[i];
                    if (@event.IsInjected)
                        continue;

                    AddAttribute(@event.Adder, @event.DefaultName, il2CppEventAttributeConstructor, il2CppMemberAttributeName);
                    AddAttribute(@event.Remover, @event.DefaultName, il2CppEventAttributeConstructor, il2CppMemberAttributeName);
                    AddAttribute(@event.Invoker, @event.DefaultName, il2CppEventAttributeConstructor, il2CppMemberAttributeName);

                    type.Events.RemoveAt(i);
                }
            }
        }
    }

    private static void AddAttribute(MethodAnalysisContext? method, string name, MethodAnalysisContext il2CppEventAttributeConstructor, PropertyAnalysisContext il2CppMemberAttributeName)
    {
        if (method is not null)
        {
            var attribute = new AnalyzedCustomAttribute(il2CppEventAttributeConstructor);
            var parameter = new CustomAttributePrimitiveParameter(name, attribute, CustomAttributeParameterKind.Property, 0);
            attribute.Properties.Add(new CustomAttributeProperty(il2CppMemberAttributeName, parameter));
            method.CustomAttributes ??= new(1);
            method.CustomAttributes.Add(attribute);

            method.OverrideAttributes = method.Attributes & ~MethodAttributes.SpecialName;
        }
    }
}
