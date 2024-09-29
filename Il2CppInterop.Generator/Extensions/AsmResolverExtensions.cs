using AsmResolver.DotNet;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace Il2CppInterop.Generator.Extensions;

internal static class AsmResolverExtensions
{
    /// <summary>
    /// Boolean, Char, or numeric type
    /// </summary>
    public static bool IsPrimitive(this TypeSignature type)
    {
        return (type as CorLibTypeSignature)?.ElementType.IsPrimitive() ?? false;
    }

    /// <summary>
    /// Boolean, Char, or numeric type
    /// </summary>
    public static bool IsPrimitive(this ElementType elementType)
    {
        return elementType is >= ElementType.Boolean and <= ElementType.R8 or ElementType.I or ElementType.U;
    }

    public static TypeSignature GetElementType(this TypeSignature type) => type switch
    {
        TypeSpecificationSignature specification => specification.BaseType,
        _ => type,
    };

    public static void AddLoadArgument(this ILProcessor instructions, int argumentIndex)
    {
        instructions.Add(OpCodes.Ldarg, instructions.GetArgument(argumentIndex));
    }

    public static void AddLoadArgumentAddress(this ILProcessor instructions, int argumentIndex)
    {
        instructions.Add(OpCodes.Ldarga, instructions.GetArgument(argumentIndex));
    }

    private static Parameter GetArgument(this ILProcessor instructions, int argumentIndex)
    {
        var method = instructions.Owner.Owner;
        return method.IsStatic
            ? method.Parameters[argumentIndex]
            : argumentIndex == 0
                ? method.Parameters.ThisParameter!
                : method.Parameters[argumentIndex - 1];
    }

    public static bool IsReferenceType(this TypeDefinition type) => !type.IsValueType;

    public static bool HasGenericParameters(this TypeDefinition type) => type.GenericParameters.Count > 0;

    public static bool HasGenericParameters(this MethodDefinition method) => method.GenericParameters.Count > 0;

    public static bool HasConstant(this FieldDefinition field) => field.Constant is not null;

    public static bool IsInstance(this FieldDefinition field) => !field.IsStatic;

    public static bool HasMethods(this TypeDefinition type) => type.Methods.Count > 0;

    public static bool HasFields(this TypeDefinition type) => type.Fields.Count > 0;

    public static bool IsNested(this ITypeDefOrRef type) => type.DeclaringType is not null;

    public static bool IsNested(this TypeSignature type) => type.DeclaringType is not null;

    public static bool IsPointerLike(this TypeSignature type) => type is PointerTypeSignature or ByReferenceTypeSignature;

    public static bool IsValueTypeLike(this TypeSignature type) => type.IsValueType || type.IsPointerLike();

    public static bool IsSystemEnum(this GenericParameterConstraint constraint) => constraint.Constraint?.FullName is "System.Enum";

    public static bool IsSystemValueType(this GenericParameterConstraint constraint) => constraint.Constraint?.FullName is "System.ValueType";

    public static bool IsInterface(this GenericParameterConstraint constraint) => constraint.Constraint?.Resolve()?.IsInterface == true;

    public static ITypeDefOrRef? AttributeType(this CustomAttribute attribute) => attribute.Constructor?.DeclaringType;

    public static Parameter AddParameter(this MethodDefinition method, TypeSignature parameterSignature, string parameterName, ParameterAttributes parameterAttributes = default)
    {
        var parameter = method.AddParameter(parameterSignature);
        var parameterDefinition = parameter.GetOrCreateDefinition();
        parameterDefinition.Name = parameterName;
        parameterDefinition.Attributes = parameterAttributes;
        return parameter;
    }

    public static Parameter AddParameter(this MethodDefinition method, TypeSignature parameterSignature)
    {
        method.Signature!.ParameterTypes.Add(parameterSignature);
        method.Parameters.PullUpdatesFromMethodSignature();
        return method.Parameters[method.Parameters.Count - 1];
    }

    public static TypeDefinition GetType(this ModuleDefinition module, string fullName)
    {
        return module.TopLevelTypes.First(t => t.FullName == fullName);
    }

    public static GenericParameterSignature ToTypeSignature(this GenericParameter genericParameter)
    {
        return new GenericParameterSignature(genericParameter.Owner is ITypeDescriptor ? GenericParameterType.Type : GenericParameterType.Method, genericParameter.Number);
    }
}
