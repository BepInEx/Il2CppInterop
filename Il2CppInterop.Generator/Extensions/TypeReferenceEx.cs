using Mono.Cecil;

namespace Il2CppInterop.Generator.Extensions;

public static class TypeReferenceEx
{
    public static bool UnmangledNamesMatch(this TypeReference typeRefA, TypeReference typeRefB)
    {
        var aIsDefOrRef = typeRefA.GetType() == typeof(TypeReference) || typeRefA.GetType() == typeof(TypeDefinition);
        var bIsDefOrRef = typeRefB.GetType() == typeof(TypeReference) || typeRefB.GetType() == typeof(TypeDefinition);
        if (!(aIsDefOrRef && bIsDefOrRef) && typeRefA.GetType() != typeRefB.GetType())
            return false;

        switch (typeRefA)
        {
            case PointerType pointer:
                return pointer.ElementType.UnmangledNamesMatch(((PointerType)typeRefB).ElementType);
            case ByReferenceType byRef:
                return byRef.ElementType.UnmangledNamesMatch(((ByReferenceType)typeRefB).ElementType);
            case ArrayType array:
                return array.ElementType.UnmangledNamesMatch(((ArrayType)typeRefB).ElementType);
            case GenericInstanceType genericInstance:
                {
                    var elementA = genericInstance.ElementType;
                    var genericInstanceB = (GenericInstanceType)typeRefB;
                    var elementB = genericInstanceB.ElementType;
                    if (!elementA.UnmangledNamesMatch(elementB))
                        return false;
                    if (genericInstance.GenericArguments.Count != genericInstanceB.GenericArguments.Count)
                        return false;

                    for (var i = 0; i < genericInstance.GenericArguments.Count; i++)
                        if (!genericInstance.GenericArguments[i].UnmangledNamesMatch(genericInstanceB.GenericArguments[i]))
                            return false;

                    return true;
                }
            default:
                return typeRefA.Name == typeRefB.Name;
        }
    }

    public static string GetNamespacePrefix(this TypeReference type)
    {
        if (type.IsNested)
            return GetNamespacePrefix(type.DeclaringType) + "." + type.DeclaringType.Name;

        return type.Namespace;
    }

    public static MethodReference DefaultCtorFor(this TypeReference type)
    {
        var resolved = type.Resolve();
        if (resolved == null)
            return null;

        var ctor = resolved.Methods.SingleOrDefault(m => m.IsConstructor && m.Parameters.Count == 0 && !m.IsStatic);
        if (ctor == null)
            return DefaultCtorFor(resolved.BaseType);

        return new MethodReference(".ctor", type.Module.TypeSystem.Void, type) { HasThis = true };
    }
}
