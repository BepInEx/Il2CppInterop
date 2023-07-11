using Il2CppInterop.Generator.Contexts;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Extensions;

public static class EnumEx
{
    public static FieldAttributes ForcePublic(this FieldAttributes fieldAttributes)
    {
        return (fieldAttributes & ~FieldAttributes.FieldAccessMask & ~FieldAttributes.HasFieldMarshal) |
               FieldAttributes.Public;
    }

    public static GenericParameterAttributes StripValueTypeConstraint(
        this GenericParameterAttributes parameterAttributes)
    {
        return parameterAttributes & ~(GenericParameterAttributes.NotNullableValueTypeConstraint |
                                       GenericParameterAttributes.VarianceMask |
                                       GenericParameterAttributes.DefaultConstructorConstraint);
    }

    public static bool IsBlittable(this TypeRewriteContext.TypeSpecifics typeSpecifics)
    {
        return typeSpecifics == TypeRewriteContext.TypeSpecifics.BlittableStruct ||
               typeSpecifics == TypeRewriteContext.TypeSpecifics.GenericBlittableStruct;
    }

}
