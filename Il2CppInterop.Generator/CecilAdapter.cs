using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.DotNet.Collections;
using AsmResolver.DotNet.Signatures;
using AsmResolver.DotNet.Signatures.Types;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables.Rows;

namespace Il2CppInterop.Generator;
internal static class CecilAdapter
{
    public static bool IsPrimitive(this TypeSignature type)
    {
        return type is CorLibTypeSignature { ElementType: >= ElementType.Boolean and <= ElementType.R8 };
    }

    public static TypeSignature GetElementType(this TypeSignature type) => type switch
    {
        TypeSpecificationSignature specification => specification.BaseType,
        _ => type,
    };

    public static void AddLoadArgument(this CilInstructionCollection instructions, int argumentIndex)
    {
        switch (argumentIndex)
        {
            case 0:
                instructions.Add(CilOpCodes.Ldarg_0);
                break;
            case 1:
                instructions.Add(CilOpCodes.Ldarg_1);
                break;
            case 2:
                instructions.Add(CilOpCodes.Ldarg_2);
                break;
            case 3:
                instructions.Add(CilOpCodes.Ldarg_3);
                break;
            default:
                instructions.Add(CilOpCodes.Ldarg, instructions.GetArgument(argumentIndex));
                break;
        }
    }

    public static void AddLoadArgumentAddress(this CilInstructionCollection instructions, int argumentIndex)
    {
        instructions.Add(CilOpCodes.Ldarga, instructions.GetArgument(argumentIndex));
    }

    private static Parameter GetArgument(this CilInstructionCollection instructions, int argumentIndex)
    {
        var method = instructions.Owner.Owner;
        return method.IsStatic
            ? method.Parameters[argumentIndex]
            : argumentIndex == 0
                ? method.Parameters.ThisParameter!
                : method.Parameters[argumentIndex - 1];
    }

    public static bool HasGenericParameters(this TypeDefinition type) => type.GenericParameters.Count > 0;

    public static bool HasGenericParameters(this MethodDefinition method) => method.GenericParameters.Count > 0;

    public static bool HasConstant(this FieldDefinition field) => field.Constant is not null;

    public static bool HasMethods(this TypeDefinition type) => type.Methods.Count > 0;

    public static bool HasFields(this TypeDefinition type) => type.Fields.Count > 0;

    public static bool IsNested(this ITypeDefOrRef type) => type.DeclaringType is not null;

    public static bool IsNested(this TypeSignature type) => type.DeclaringType is not null;

    public static ITypeDefOrRef? AttributeType(this CustomAttribute attribute) => attribute.Constructor?.DeclaringType;

    public static bool HasOverrides(this MethodDefinition method)
    {
#warning TODO: Implement
        //throw new NotImplementedException();
        return false;
    }

    public static MethodSignature CreateMethodSignature(MethodAttributes attributes, TypeSignature returnType, params TypeSignature[] parameterTypes)
    {
        return CreateMethodSignature((attributes & MethodAttributes.Static) != 0, returnType, parameterTypes);
    }

    public static MethodSignature CreateMethodSignature(bool isStatic, TypeSignature returnType, params TypeSignature[] parameterTypes)
    {
        return isStatic ? MethodSignature.CreateStatic(returnType, parameterTypes) : MethodSignature.CreateInstance(returnType, parameterTypes);
    }

    public static Parameter AddParameter(this MethodDefinition method, TypeSignature parameterSignature, string parameterName, ParameterAttributes parameterAttributes = default)
    {
        ParameterDefinition parameterDefinition = new ParameterDefinition((ushort)(method.Signature!.ParameterTypes.Count + 1), parameterName, parameterAttributes);
        method.Signature.ParameterTypes.Add(parameterSignature);
        method.ParameterDefinitions.Add(parameterDefinition);

        method.Parameters.PullUpdatesFromMethodSignature();
        return method.Parameters.Single(parameter => parameter.Name == parameterName && parameter.ParameterType == parameterSignature);
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

    public static MemberReference CreateFieldReference(Utf8String? name, TypeSignature fieldType, IMemberRefParent? parent)
    {
        return new MemberReference(parent, name, new FieldSignature(fieldType));
    }

    public static MemberReference CreateInstanceMethodReference(Utf8String? name, TypeSignature returnType, IMemberRefParent? parent, params TypeSignature[] parameterTypes)
    {
        return new MemberReference(parent, name, MethodSignature.CreateInstance(returnType, parameterTypes));
    }

    public static MemberReference CreateStaticMethodReference(Utf8String? name, TypeSignature returnType, IMemberRefParent? parent, params TypeSignature[] parameterTypes)
    {
        return new MemberReference(parent, name, MethodSignature.CreateStatic(returnType, parameterTypes));
    }

    public static GenericParameterSignature ToTypeSignature(this GenericParameter genericParameter)
    {
        return new GenericParameterSignature(genericParameter.Owner is ITypeDescriptor ? GenericParameterType.Type : GenericParameterType.Method, genericParameter.Number);
    }
}
