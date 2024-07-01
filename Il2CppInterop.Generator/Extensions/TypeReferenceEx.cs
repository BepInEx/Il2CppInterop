using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.Extensions;

public static class TypeReferenceEx
{
    public static bool UnmangledNamesMatch(this TypeSignature typeRefA, TypeSignature typeRefB)
    {
        var aIsDefOrRef = typeRefA.GetType() == typeof(TypeDefOrRefSignature);
        var bIsDefOrRef = typeRefB.GetType() == typeof(TypeDefOrRefSignature);
        if (!(aIsDefOrRef && bIsDefOrRef) && typeRefA.GetType() != typeRefB.GetType())
            return false;

        switch (typeRefA)
        {
            case PointerTypeSignature pointer:
                return pointer.BaseType.UnmangledNamesMatch(((PointerTypeSignature)typeRefB).BaseType);
            case ByReferenceTypeSignature byRef:
                return byRef.BaseType.UnmangledNamesMatch(((ByReferenceTypeSignature)typeRefB).BaseType);
            case ArrayBaseTypeSignature array:
                return array.BaseType.UnmangledNamesMatch(((ArrayBaseTypeSignature)typeRefB).BaseType);
            case GenericInstanceTypeSignature genericInstance:
                {
                    var elementA = genericInstance.GenericType.ToTypeSignature();
                    var genericInstanceB = (GenericInstanceTypeSignature)typeRefB;
                    var elementB = genericInstanceB.GenericType.ToTypeSignature();
                    if (!elementA.UnmangledNamesMatch(elementB))
                        return false;
                    if (genericInstance.TypeArguments.Count != genericInstanceB.TypeArguments.Count)
                        return false;

                    for (var i = 0; i < genericInstance.TypeArguments.Count; i++)
                        if (!genericInstance.TypeArguments[i].UnmangledNamesMatch(genericInstanceB.TypeArguments[i]))
                            return false;

                    return true;
                }
            default:
                return typeRefA.Name == typeRefB.Name;
        }
    }

    public static string? GetNamespacePrefix(this ITypeDefOrRef type)
    {
        if (type.DeclaringType is not null)
            return $"{GetNamespacePrefix(type.DeclaringType)}.{type.DeclaringType.Name}";

        return type.Namespace;
    }
}
