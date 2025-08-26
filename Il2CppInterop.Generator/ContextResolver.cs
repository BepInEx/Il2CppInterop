using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;
using LibCpp2IL.BinaryStructures;

namespace Il2CppInterop.Generator;

public readonly struct ContextResolver
{
    private readonly AssemblyAnalysisContext referencedFrom;
    private readonly TypeAnalysisContext? referencingType;
    private readonly MethodAnalysisContext? referencingMethod;

    public ContextResolver(AssemblyAnalysisContext referencedFrom)
    {
        this.referencedFrom = referencedFrom;
    }

    public ContextResolver(TypeAnalysisContext referencingType)
    {
        if (referencingType is ReferencedTypeAnalysisContext)
            throw new ArgumentException("Must be a simple type", nameof(referencingType));
        referencedFrom = referencingType.DeclaringAssembly;
        this.referencingType = referencingType;
    }

    public ContextResolver(MethodAnalysisContext referencingMethod)
    {
        if (referencingMethod is ConcreteGenericMethodAnalysisContext)
            throw new ArgumentException("Must be a simple method", nameof(referencingMethod));
        referencedFrom = referencingMethod.CustomAttributeAssembly;
        referencingType = referencingMethod.DeclaringType;
        this.referencingMethod = referencingMethod;
    }

    public TypeAnalysisContext? Resolve(TypeSignature? type) => type switch
    {
        // Ordered roughly by frequency
        TypeDefOrRefSignature typeDefOrRef => Resolve(typeDefOrRef),
        CorLibTypeSignature primitive => Resolve(primitive),
        GenericInstanceTypeSignature genericInstance => Resolve(genericInstance),
        SzArrayTypeSignature szArray => Resolve(szArray.BaseType)?.MakeSzArrayType(),
        GenericParameterSignature genericParameter => genericParameter.ParameterType switch
        {
            GenericParameterType.Type => TryGetGenericParameter(referencingType?.GenericParameters, genericParameter.Index),
            _ => TryGetGenericParameter(referencingMethod?.GenericParameters, genericParameter.Index),
        },
        ByReferenceTypeSignature byRef => Resolve(byRef.BaseType)?.MakeByReferenceType(),
        PointerTypeSignature pointer => Resolve(pointer.BaseType)?.MakePointerType(),
        ArrayTypeSignature array => Resolve(array.BaseType)?.MakeArrayType(array.Rank),
        PinnedTypeSignature pinned => Resolve(pinned.BaseType)?.MakePinnedType(),
        CustomModifierTypeSignature customModifier => Resolve(customModifier),
        BoxedTypeSignature boxed => Resolve(boxed.BaseType)?.MakeBoxedType(),
        SentinelTypeSignature => new SentinelTypeAnalysisContext(referencedFrom),
        _ => null
    };

    private static GenericParameterTypeAnalysisContext? TryGetGenericParameter(List<GenericParameterTypeAnalysisContext>? genericParameters, int index)
    {
        if (genericParameters is null || index < 0 || index >= genericParameters.Count)
            return null;
        return genericParameters[index];
    }

    public bool TryResolve(TypeSignature? type, [NotNullWhen(true)] out TypeAnalysisContext? result)
    {
        result = Resolve(type);
        return result is not null;
    }

    private GenericInstanceTypeAnalysisContext? Resolve(GenericInstanceTypeSignature genericInstance)
    {
        return TryResolve(genericInstance.GenericType, out var genericType) && TryResolve(genericInstance.TypeArguments, out var genericArguments)
            ? new GenericInstanceTypeAnalysisContext(genericType, genericArguments, referencedFrom)
            : null;
    }

    private CustomModifierTypeAnalysisContext? Resolve(CustomModifierTypeSignature customModifier)
    {
        return TryResolve(customModifier.BaseType, out var baseType) && TryResolve(customModifier.ModifierType, out var modifier)
            ? new CustomModifierTypeAnalysisContext(baseType, modifier, customModifier.IsRequired, referencedFrom)
            : null;
    }

    private TypeAnalysisContext? Resolve(TypeDefOrRefSignature typeDefOrRef)
    {
        if (typeDefOrRef.DeclaringType is not null)
        {
            if (!TryResolve(typeDefOrRef.DeclaringType, out var declaringType))
                return null;

            foreach (var nestedType in declaringType.NestedTypes)
            {
                if (nestedType.Name == typeDefOrRef.Name)
                {
                    return nestedType;
                }
            }

            return null;
        }

        if (typeDefOrRef.Type is not TypeDefinition)
        {
            typeDefOrRef = (TypeDefOrRefSignature?)typeDefOrRef.Resolve()?.ToTypeSignature() ?? typeDefOrRef;
        }

        var assemblyName = GetName(typeDefOrRef.Scope);
        if (assemblyName == null)
            return null;

        var targetAssembly = referencedFrom.AppContext.Assemblies.FirstOrDefault(a => a.Name == assemblyName);
        if (targetAssembly == null)
            return null;

        return targetAssembly.GetTypeByFullName(typeDefOrRef.FullName);

        static string? GetName(IResolutionScope? scope) => scope switch
        {
            ModuleDefinition module => module.Assembly!.Name,
            AssemblyReference assembly => assembly.Name,
            _ => throw new NotImplementedException(),
        };
    }

    private TypeAnalysisContext? Resolve(CorLibTypeSignature corLibType)
    {
        return referencedFrom.AppContext.Mscorlib.GetTypeByFullName(corLibType.FullName);
    }

    public TypeAnalysisContext? Resolve(ITypeDescriptor? type)
    {
        return Resolve(type?.ToTypeSignature());
    }

    public bool TryResolve(ITypeDescriptor? type, [NotNullWhen(true)] out TypeAnalysisContext? result)
    {
        result = Resolve(type);
        return result is not null;
    }

    public IEnumerable<TypeAnalysisContext?> Resolve(IEnumerable<TypeSignature?> types)
    {
        foreach (var type in types)
        {
            yield return Resolve(type);
        }
    }

    public bool TryResolve(IEnumerable<TypeSignature?> types, [NotNullWhen(true)] out List<TypeAnalysisContext>? result)
    {
        result = [];
        foreach (var type in types)
        {
            if (TryResolve(type, out var resolvedType))
            {
                result.Add(resolvedType);
            }
            else
            {
                result = null;
                return false;
            }
        }
        return true;
    }

    public FieldAnalysisContext? Resolve(IFieldDescriptor fieldDescriptor)
    {
        var declaringType = Resolve(fieldDescriptor.DeclaringType?.ToTypeSignature());
        if (declaringType is null)
            return null;

        if (declaringType is GenericInstanceTypeAnalysisContext genericInstanceType)
        {
            var baseField = genericInstanceType.GenericType.TryGetFieldByName(fieldDescriptor.Name);
            if (baseField is null)
                return null;

            return new ConcreteGenericFieldAnalysisContext(baseField, genericInstanceType);
        }
        else
        {
            return declaringType.TryGetFieldByName(fieldDescriptor.Name);
        }
    }

    public bool TryResolve(IFieldDescriptor fieldDescriptor, [NotNullWhen(true)] out FieldAnalysisContext? result)
    {
        result = Resolve(fieldDescriptor);
        return result is not null;
    }

    public object? Resolve(IMethodDescriptor methodDescriptor)
    {
        if (methodDescriptor is MethodSpecification specification)
        {
            return Resolve(specification);
        }

        var methodDefOrRef = (IMethodDefOrRef)methodDescriptor;

        if (!TryResolve(methodDefOrRef.DeclaringType, out var declaringType))
            return null;

        var nonGenericDeclaringType = (declaringType as GenericInstanceTypeAnalysisContext)?.GenericType ?? declaringType;

        if (nonGenericDeclaringType is ArrayTypeAnalysisContext arrayDeclaringType)
        {
            Debug.Assert(nonGenericDeclaringType == declaringType, "Array types should not be generic instances");
            return methodDefOrRef.Name?.Value switch
            {
                "Get" => new MultiDimensionalArrayMethod(arrayDeclaringType, MultiDimensionalArrayMethodType.Get),
                "Set" => new MultiDimensionalArrayMethod(arrayDeclaringType, MultiDimensionalArrayMethodType.Set),
                "Address" => new MultiDimensionalArrayMethod(arrayDeclaringType, MultiDimensionalArrayMethodType.Address),
                ".ctor" => new MultiDimensionalArrayMethod(arrayDeclaringType, MultiDimensionalArrayMethodType.Constructor),
                _ => null,
            };
        }

        Debug.Assert(nonGenericDeclaringType is not ReferencedTypeAnalysisContext);

        var targetMethod = new ContextResolver(nonGenericDeclaringType).ResolveInType(methodDefOrRef);
        if (targetMethod is null)
            return null;

        if (declaringType is GenericInstanceTypeAnalysisContext genericInstanceType)
        {
            return new ConcreteGenericMethodAnalysisContext(targetMethod, genericInstanceType.GenericArguments, []);
        }
        else
        {
            return targetMethod;
        }
    }

    public bool TryResolve(IMethodDescriptor methodDescriptor, [NotNullWhen(true)] out object? result)
    {
        result = Resolve(methodDescriptor);
        return result is not null;
    }

    public MethodAnalysisContext? Resolve(MethodDefinition methodDefinition)
    {
        // The declaring type can be resolved with nothing, but resolution for the method itself requires a context.
        return TryResolve(methodDefinition.DeclaringType, out var declaringType) ? new ContextResolver(declaringType).ResolveInType(methodDefinition) : null;
    }

    public MethodAnalysisContext? ResolveInType(IMethodDefOrRef methodDefOrRef)
    {
        if (referencingType is null)
            throw new InvalidOperationException("Cannot resolve method in type without a referencing type");

        if (methodDefOrRef.Signature is null || methodDefOrRef.Signature.SentinelParameterTypes.Count > 0)
            return null;

        foreach (var methodContext in referencingType.Methods)
        {
            if (methodContext.Name != methodDefOrRef.Name)
                continue;

            if (methodContext.Parameters.Count != methodDefOrRef.Signature.ParameterTypes.Count)
                continue;

            if (methodContext.GenericParameters.Count != methodDefOrRef.Signature.GenericParameterCount)
                continue;

            if (methodContext.IsStatic == methodDefOrRef.Signature.HasThis)
                continue;

            if (methodContext.IsVoid == methodDefOrRef.Signature.ReturnsValue)
                continue;

            // We need to use a resolver for the method context to resolve potential method generic parameters correctly.
            var methodResolver = new ContextResolver(methodContext);

            if (!methodResolver.TryResolve(methodDefOrRef.Signature.ReturnType, out var returnType) ||
                !TypeAnalysisContextEqualityComparer.Instance.Equals(methodContext.ReturnType, returnType))
                continue;

            if (!methodResolver.TryResolve(methodDefOrRef.Signature.ParameterTypes, out var parameterTypes) ||
                !methodContext.Parameters.Select(p => p.ParameterType).SequenceEqual(parameterTypes, TypeAnalysisContextEqualityComparer.Instance))
                continue;

            return methodContext;
        }

        return null;
    }

    public ConcreteGenericMethodAnalysisContext? Resolve(MethodSpecification specification)
    {
        if (specification.Method is null || specification.Signature is null)
            return null;

        var baseMethod = (MethodAnalysisContext?)Resolve(specification.Method);
        if (baseMethod is null or { DeclaringType: null })
            return null;

        if (!TryResolve(specification.Signature.TypeArguments, out var methodTypeArguments))
            return null;

        return baseMethod.MakeGenericInstanceMethod(methodTypeArguments);
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
}
