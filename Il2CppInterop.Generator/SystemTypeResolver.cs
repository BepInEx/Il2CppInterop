using System.Diagnostics;
using Cpp2IL.Core.Model.Contexts;

namespace Il2CppInterop.Generator;

public readonly struct SystemTypeResolver
{
    private readonly AssemblyAnalysisContext referencedFrom;
    private readonly TypeAnalysisContext? referencingType;
    private readonly MethodAnalysisContext? referencingMethod;

    public SystemTypeResolver(AssemblyAnalysisContext referencedFrom)
    {
        this.referencedFrom = referencedFrom;
    }

    public SystemTypeResolver(TypeAnalysisContext referencingType)
    {
        if (referencingType is ReferencedTypeAnalysisContext)
            throw new ArgumentException("Must be a simple type", nameof(referencingType));
        referencedFrom = referencingType.DeclaringAssembly;
        this.referencingType = referencingType;
    }

    public SystemTypeResolver(MethodAnalysisContext referencingMethod)
    {
        if (referencingMethod is ConcreteGenericMethodAnalysisContext)
            throw new ArgumentException("Must be a simple method", nameof(referencingMethod));
        referencedFrom = referencingMethod.CustomAttributeAssembly;
        referencingType = referencingMethod.DeclaringType;
        this.referencingMethod = referencingMethod;
    }
    public TypeAnalysisContext ResolveOrThrow(Type? type, bool allowGenericInstance = true)
    {
        return Resolve(type, allowGenericInstance) ?? throw new($"Unable to resolve type {type?.FullName}");
    }

    public TypeAnalysisContext? Resolve(Type? type) => Resolve(type, true);
    public TypeAnalysisContext? Resolve(Type? type, bool allowGenericInstance)
    {
        if (type is null)
            return null;

        if (type.IsSZArray)
            return Resolve(type.GetElementType())?.MakeSzArrayType();

        if (type.IsByRef)
            return Resolve(type.GetElementType())?.MakeByReferenceType();

        if (type.IsPointer)
            return Resolve(type.GetElementType())?.MakePointerType();

        if (type.IsArray)
            return Resolve(type.GetElementType())?.MakeArrayType(type.GetArrayRank());

        if (type.IsGenericParameter)
        {
            if (type.IsGenericTypeParameter)
            {
                return TryGetGenericParameter(referencingType?.GenericParameters, type.GenericParameterPosition);
            }
            else
            {
                Debug.Assert(type.IsGenericMethodParameter);
                return TryGetGenericParameter(referencingMethod?.GenericParameters, type.GenericParameterPosition);
            }
        }

        if (type.IsGenericType && allowGenericInstance)
        {
            var genericArguments = type.GetGenericArguments().Select(Resolve).ToArray();
            if (genericArguments.Any(x => x is null))
                return null;

            var genericType = type.GetGenericTypeDefinition();
            return Resolve(genericType, false)?.MakeGenericInstanceType(genericArguments!);
        }

        if (type.IsFunctionPointer)
            throw new NotSupportedException($"Function pointers are not supported: {type.Name}");

        // Custom modifiers might be possible to support, but probably not necessary

        var assemblyName = type.Assembly.GetName().Name!;
        if (assemblyName == "System.Private.CoreLib")
            assemblyName = "mscorlib";
        var assembly = referencedFrom.AppContext.GetAssemblyByName(assemblyName);
        return assembly?.GetTypeByFullName(type.FullName!);
    }

    private static GenericParameterTypeAnalysisContext? TryGetGenericParameter(List<GenericParameterTypeAnalysisContext>? genericParameters, int index)
    {
        if (genericParameters is null || index < 0 || index >= genericParameters.Count)
            return null;
        return genericParameters[index];
    }
}
