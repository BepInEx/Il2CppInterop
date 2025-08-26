using AsmResolver.DotNet;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Passes;

public static class Pass71GenerateEvents
{
    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
            {
                var type = typeContext.OriginalType;
                var eventCountsByName = new Dictionary<string, int>();

                foreach (var oldEvent in type.Events)
                {
                    var unmangledEventName = UnmangleEventName(assemblyContext, oldEvent, typeContext.NewType, eventCountsByName);

                    var eventType = assemblyContext.RewriteTypeRef(oldEvent.EventType?.ToTypeSignature());
                    var @event = new EventDefinition(unmangledEventName, oldEvent.Attributes, eventType.ToTypeDefOrRef());

                    typeContext.NewType.Events.Add(@event);

                    @event.SetSemanticMethods(
                        oldEvent.AddMethod is null ? null : typeContext.GetMethodByOldMethod(oldEvent.AddMethod).NewMethod,
                        oldEvent.RemoveMethod is null ? null : typeContext.GetMethodByOldMethod(oldEvent.RemoveMethod).NewMethod,
                        oldEvent.FireMethod is null ? null : typeContext.GetMethodByOldMethod(oldEvent.FireMethod).NewMethod);
                }
            }
    }

    private static string UnmangleEventName(AssemblyRewriteContext assemblyContext, EventDefinition @event,
        ITypeDefOrRef declaringType, Dictionary<string, int> countsByBaseName)
    {
        if (assemblyContext.GlobalContext.Options.PassthroughNames ||
            !@event.Name.IsObfuscated(assemblyContext.GlobalContext.Options)) return @event.Name!;

        var baseName = "event_" + assemblyContext.RewriteTypeRef(@event.EventType?.ToTypeSignature()).GetUnmangledName(@event.DeclaringType);

        countsByBaseName.TryGetValue(baseName, out var index);
        countsByBaseName[baseName] = index + 1;

        var unmangleEventName = baseName + "_" + index;

        if (assemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
            declaringType.GetNamespacePrefix() + "." + declaringType.Name + "::" + unmangleEventName, out var newNameByType))
        {
            unmangleEventName = newNameByType;
        }
        else if (assemblyContext.GlobalContext.Options.RenameMap.TryGetValue(
            declaringType.GetNamespacePrefix() + "::" + unmangleEventName, out var newName))
        {
            unmangleEventName = newName;
        }

        return unmangleEventName;
    }
}
