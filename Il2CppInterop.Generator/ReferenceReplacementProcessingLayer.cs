using Cpp2IL.Core.Api;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class ReferenceReplacementProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Id => "reference_replacement";
    public override string Name => "Reference Replacement";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        var il2CppMscorlib = appContext.AssembliesByName["Il2Cppmscorlib"];
        var mscorlib = appContext.AssembliesByName["mscorlib"];

        var monoSystemObject = mscorlib.GetTypeByFullNameOrThrow("System.Object");
        var monoSystemValueType = mscorlib.GetTypeByFullNameOrThrow("System.ValueType");
        var monoSystemVoid = mscorlib.GetTypeByFullNameOrThrow("System.Void");

        var il2CppSystemObject = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Object");
        var il2CppSystemVoid = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Void");
        var il2CppSystemEnum = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.Enum");
        var il2CppSystemValueType = il2CppMscorlib.GetTypeByFullNameOrThrow("Il2CppSystem.ValueType");

        var visitor = TypeConversionVisitor.Create(appContext);

        il2CppSystemObject.OverrideBaseType = monoSystemObject;

        foreach (var type in appContext.AllTypes)
        {
            if (type.CustomAttributeAssembly.IsReferenceAssembly)
            {
                continue;
            }

            if (type.IsStatic || type.IsInterface)
            {
                type.OverrideBaseType = monoSystemObject;
            }
            else if (type == il2CppSystemObject || type == il2CppSystemEnum || type == il2CppSystemValueType)
            {
            }
            else if (type.BaseType is null || type.BaseType == il2CppSystemObject)
            {
                type.OverrideBaseType = il2CppSystemObject;
            }
            else if (type.BaseType == il2CppSystemValueType)
            {
                type.OverrideBaseType = monoSystemValueType;
            }
            else if (type.BaseType == il2CppSystemEnum)
            {
                type.OverrideBaseType = monoSystemValueType;
            }
            else
            {
                type.OverrideBaseType = visitor.Replace(type.BaseType);
            }

            visitor.Replace(type.InterfaceContexts);
            foreach (var genericParameter in type.GenericParameters)
            {
                visitor.Replace(genericParameter.ConstraintTypes);
            }

            foreach (var field in type.Fields)
            {
                field.OverrideFieldType = visitor.Replace(field.FieldType);
            }

            foreach (var method in type.Methods)
            {
                if (method.ReturnType == il2CppSystemVoid)
                {
                    // Special case for void return type.
                    method.OverrideReturnType = monoSystemVoid;
                }
                else
                {
                    method.OverrideReturnType = visitor.Replace(method.ReturnType);
                }
                foreach (var parameter in method.Parameters)
                {
                    parameter.OverrideParameterType = ReplaceExceptTopLevelByRef(visitor, parameter.ParameterType);
                }
                foreach (var genericParameter in method.GenericParameters)
                {
                    visitor.Replace(genericParameter.ConstraintTypes);
                }
            }

            foreach (var property in type.Properties)
            {
                property.OverridePropertyType = ReplaceExceptTopLevelByRef(visitor, property.PropertyType);
            }

            foreach (var @event in type.Events)
            {
                @event.OverrideEventType = visitor.Replace(@event.EventType);
            }
        }
    }

    private static TypeAnalysisContext ReplaceExceptTopLevelByRef(TypeConversionVisitor visitor, TypeAnalysisContext type)
    {
        if (type is ByRefTypeAnalysisContext byRefType)
        {
            var elementType = byRefType.ElementType;
            var replacedElementType = visitor.Replace(elementType);
            if (replacedElementType == elementType)
            {
                return byRefType;
            }
            else
            {
                return replacedElementType.MakeByReferenceType();
            }
        }
        else
        {
            return visitor.Replace(type);
        }
    }
}
