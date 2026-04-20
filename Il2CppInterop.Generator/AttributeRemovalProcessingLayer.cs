using Cpp2IL.Core.Api;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;
using Cpp2IL.Core.Model.CustomAttributes;

namespace Il2CppInterop.Generator;

public class AttributeRemovalProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "attribute_removal";
    public override string Name => "Attribute Removal";
    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var isUnmanagedAttributeType = appContext.GetAssemblyByName("mscorlib")?.GetTypeByFullName("System.Runtime.CompilerServices.IsUnmanagedAttribute");

        if (isUnmanagedAttributeType is null)
        {
            Logger.WarnNewline("IsUnmanagedAttribute not found. They cannot be recovered.", nameof(AttributeRemovalProcessingLayer));
        }

        var isUnmanagedAttributeConstructor = isUnmanagedAttributeType?.Methods.First(m => m.Name == ".ctor" && m.Parameters.Count == 0);

        foreach (var assembly in appContext.Assemblies)
        {
            ClearAttributes(assembly);
        }

        foreach (var type in appContext.AllTypes)
        {
            ClearAttributes(type);

            foreach (var genericParameter in type.GenericParameters)
            {
                ClearGenericParameterAttributes(genericParameter, isUnmanagedAttributeConstructor);
            }

            foreach (var field in type.Fields)
            {
                ClearAttributes(field);
            }

            foreach (var method in type.Methods)
            {
                ClearAttributes(method);

                foreach (var parameter in method.Parameters)
                {
                    ClearAttributes(parameter);
                }

                foreach (var genericParameter in method.GenericParameters)
                {
                    ClearGenericParameterAttributes(genericParameter, isUnmanagedAttributeConstructor);
                }
            }

            foreach (var property in type.Properties)
            {
                ClearAttributes(property);
            }

            foreach (var @event in type.Events)
            {
                ClearAttributes(@event);
            }
        }
    }

    private static void ClearGenericParameterAttributes(GenericParameterTypeAnalysisContext genericParameter, MethodAnalysisContext? isUnmanagedAttributeConstructor)
    {
        if (isUnmanagedAttributeConstructor is not null && HasIsUnmanagedAttribute(genericParameter))
        {
            ClearAttributes(genericParameter);
            genericParameter.CustomAttributes ??= new(1);
            genericParameter.CustomAttributes.Add(new AnalyzedCustomAttribute(isUnmanagedAttributeConstructor));
        }
        else
        {
            ClearAttributes(genericParameter);
        }

        static bool HasIsUnmanagedAttribute(GenericParameterTypeAnalysisContext genericParameter)
        {
            return genericParameter.HasCustomAttributeWithFullName("System.Runtime.CompilerServices.IsUnmanagedAttribute")
                || genericParameter.HasCustomAttributeWithFullName("Il2CppSystem.Runtime.CompilerServices.IsUnmanagedAttribute");
        }
    }

    private static void ClearAttributes(HasCustomAttributes context)
    {
        context.AttributeTypes?.Clear();
        context.CustomAttributes?.Clear();
    }
}
