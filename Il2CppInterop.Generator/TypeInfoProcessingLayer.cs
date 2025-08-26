using System.Diagnostics;
using Cpp2IL.Core.Api;
using Cpp2IL.Core.Logging;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public class TypeInfoProcessingLayer : Cpp2IlProcessingLayer
{
    public override string Name => "Record information about each type";

    public override string Id => "type_info";

    public override void Process(ApplicationAnalysisContext appContext, Action<int, int>? progressCallback = null)
    {
        Logger.InfoNewline("Processing type information...", nameof(TypeInfoProcessingLayer));

        foreach (var type in appContext.AllTypes)
        {
            var typeInfo = new Il2CppTypeInfo();
            if (!type.IsValueType)
            {
                typeInfo.Blittability = TypeBlittability.ReferenceType;
            }
            foreach (var field in type.Fields)
            {
                if (field.IsStatic)
                {
                    typeInfo.StaticFields.Add(field);
                }
                else
                {
                    typeInfo.InstanceFields.Add(field);
                }
            }
            type.PutExtraData(typeInfo);
        }
        bool changed;
        do
        {
            changed = false;

            foreach (var type in appContext.AllTypes)
            {
                var typeInfo = type.GetExtraData<Il2CppTypeInfo>()!;
                if (typeInfo.Blittability is not TypeBlittability.Unknown)
                    continue;

                Debug.Assert(type.IsValueType);

                var anyNonBlittable = false;
                var anyUnknown = false;
                foreach (var field in typeInfo.InstanceFields)
                {
                    var fieldType = GetUnderlyingType(field.FieldType);

                    if (fieldType is ArrayTypeAnalysisContext or SzArrayTypeAnalysisContext or PointerTypeAnalysisContext or ByRefTypeAnalysisContext)
                    {
                        // Reference types are "blittable" because they are represented as pointers in C++.
                        continue;
                    }

                    Debug.Assert(fieldType is not PinnedTypeAnalysisContext and not BoxedTypeAnalysisContext and not SentinelTypeAnalysisContext);

                    if (fieldType is GenericParameterTypeAnalysisContext genericParameter)
                    {
                        if (genericParameter.Attributes.HasFlag(System.Reflection.GenericParameterAttributes.ReferenceTypeConstraint))
                        {
                            // Reference types are "blittable" because they are represented as pointers in C++.
                        }
                        else if (genericParameter.Attributes.HasFlag(System.Reflection.GenericParameterAttributes.NotNullableValueTypeConstraint) &&
                            genericParameter.HasCustomAttributeWithFullName("System.Runtime.CompilerServices.IsUnmanagedAttribute"))
                        {
                            // Blittable
                        }
                        else
                        {
                            // Non-blittable because a non-blittable struct could be used as the generic argument.
                            anyNonBlittable = true;
                            break;
                        }
                    }
                    else if (fieldType == type)
                    {
                        // Corlib primitives reference themselves. We can ignore this.
                    }
                    else
                    {
                        var fieldTypeInfo = fieldType.GetExtraData<Il2CppTypeInfo>()!;
                        if (fieldTypeInfo.Blittability == TypeBlittability.NonBlittableValueType)
                        {
                            anyNonBlittable = true;
                            break;
                        }
                        else if (fieldTypeInfo.Blittability == TypeBlittability.Unknown)
                        {
                            anyUnknown = true;
                        }
                    }
                }
                if (anyNonBlittable)
                {
                    typeInfo.Blittability = TypeBlittability.NonBlittableValueType;
                    changed = true;
                }
                else if (!anyUnknown)
                {
                    typeInfo.Blittability = TypeBlittability.BlittableValueType;
                    changed = true;
                }
            }
        } while (changed);
    }

    private static TypeAnalysisContext GetUnderlyingType(TypeAnalysisContext type) => type switch
    {
        GenericInstanceTypeAnalysisContext genericInstance => GetUnderlyingType(genericInstance.GenericType),
        CustomModifierTypeAnalysisContext customModifier => GetUnderlyingType(customModifier.ElementType),
        _ => type,
    };
}
