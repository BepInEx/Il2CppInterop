using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.MetadataAccess;

/// <summary>
/// Custom metadata resolver for HybridCLR environments.
/// Handles type relocation issues where some types are moved from System.dll to mscorlib by IL2CPP.
/// </summary>
internal sealed class HybridCLRMetadataResolver : IMetadataResolver
{
    private readonly IAssemblyResolver _assemblyResolver;
    private readonly Il2CppAssemblyResolver? _il2cppResolver;

    public HybridCLRMetadataResolver(IAssemblyResolver assemblyResolver)
    {
        _assemblyResolver = assemblyResolver ?? throw new ArgumentNullException(nameof(assemblyResolver));
        _il2cppResolver = assemblyResolver as Il2CppAssemblyResolver;
    }

    public IAssemblyResolver AssemblyResolver => _assemblyResolver;

    /// <summary>
    /// Resolves an assembly, using Il2CppAssemblyResolver's signature-ignoring lookup if available.
    /// </summary>
    private AssemblyDefinition? ResolveAssembly(AssemblyDescriptor descriptor)
    {
        // Use Il2CppAssemblyResolver's Resolve method which ignores signature mismatches
        if (_il2cppResolver != null)
            return _il2cppResolver.Resolve(descriptor);

        return _assemblyResolver.Resolve(descriptor);
    }

    public TypeDefinition? ResolveType(ITypeDescriptor? type)
    {
        if (type is null)
            return null;

        return type switch
        {
            TypeDefinition definition => definition,
            TypeReference reference => ResolveTypeReference(reference),
            TypeSpecification specification => ResolveType(specification.Signature?.GetUnderlyingTypeDefOrRef()),
            ExportedType exportedType => ResolveExportedType(exportedType),
            _ => null
        };
    }

    public MethodDefinition? ResolveMethod(IMethodDescriptor? method)
    {
        if (method is null)
            return null;

        if (method is MethodDefinition definition)
            return definition;

        if (method is not IMethodDefOrRef methodDefOrRef)
            return null;

        var declaringType = ResolveType(methodDefOrRef.DeclaringType);
        if (declaringType is null)
            return null;

        return FindMethodInType(declaringType, methodDefOrRef);
    }

    public FieldDefinition? ResolveField(IFieldDescriptor? field)
    {
        if (field is null)
            return null;

        if (field is FieldDefinition definition)
            return definition;

        if (field is not MemberReference memberRef)
            return null;

        var declaringType = ResolveType(memberRef.DeclaringType);
        if (declaringType is null)
            return null;

        return FindFieldInType(declaringType, memberRef);
    }

    private TypeDefinition? ResolveTypeReference(TypeReference reference)
    {
        var scope = reference.Scope;
        if (scope is null)
            return null;

        AssemblyDefinition? assembly = null;

        switch (scope)
        {
            case AssemblyDescriptor assemblyDescriptor:
                assembly = ResolveAssembly(assemblyDescriptor);
                break;
            case ModuleDefinition module:
                assembly = module.Assembly;
                break;
            case TypeReference declaringType:
                var resolvedDeclaringType = ResolveType(declaringType);
                if (resolvedDeclaringType is not null)
                    return FindNestedType(resolvedDeclaringType, reference.Name);
                break;
        }

        if (assembly is null)
            return null;

        // Try to find the type in the resolved assembly
        var result = FindTypeInAssembly(assembly, reference.Namespace, reference.Name);
        if (result is not null)
            return result;

        // HybridCLR/IL2CPP type relocation fix:
        // Some types are moved from System.dll to mscorlib by IL2CPP.
        // If we can't find the type in the original assembly, try mscorlib as fallback.
        if (!IsCorLib(assembly))
        {
            var corLib = GetCorLibAssembly();
            if (corLib is not null)
            {
                result = FindTypeInAssembly(corLib, reference.Namespace, reference.Name);
                if (result is not null)
                    return result;
            }
        }

        return null;
    }

    private TypeDefinition? ResolveExportedType(ExportedType exportedType)
    {
        var implementation = exportedType.Implementation;
        if (implementation is null)
            return null;

        switch (implementation)
        {
            case AssemblyReference assemblyRef:
                var assembly = ResolveAssembly(assemblyRef);
                if (assembly is not null)
                    return FindTypeInAssembly(assembly, exportedType.Namespace, exportedType.Name);
                break;
            case ExportedType parentExportedType:
                var parentType = ResolveExportedType(parentExportedType);
                if (parentType is not null)
                    return FindNestedType(parentType, exportedType.Name);
                break;
        }

        return null;
    }

    private TypeDefinition? FindTypeInAssembly(AssemblyDefinition assembly, Utf8String? ns, Utf8String? name)
    {
        if (name is null)
            return null;

        for (int i = 0; i < assembly.Modules.Count; i++)
        {
            var module = assembly.Modules[i];
            var type = FindTypeInModule(module, ns, name);
            if (type is not null)
                return type;
        }

        return null;
    }

    private static TypeDefinition? FindTypeInModule(ModuleDefinition module, Utf8String? ns, Utf8String? name)
    {
        foreach (var type in module.TopLevelTypes)
        {
            if (type.Name == name && type.Namespace == ns)
                return type;
        }

        return null;
    }

    private static TypeDefinition? FindNestedType(TypeDefinition declaringType, Utf8String? name)
    {
        if (name is null)
            return null;

        foreach (var nestedType in declaringType.NestedTypes)
        {
            if (nestedType.Name == name)
                return nestedType;
        }

        return null;
    }

    private static MethodDefinition? FindMethodInType(TypeDefinition type, IMethodDefOrRef methodRef)
    {
        foreach (var method in type.Methods)
        {
            if (method.Name == methodRef.Name && SignatureComparer.Default.Equals(method.Signature, methodRef.Signature))
                return method;
        }

        return null;
    }

    private static FieldDefinition? FindFieldInType(TypeDefinition type, MemberReference fieldRef)
    {
        foreach (var field in type.Fields)
        {
            if (field.Name == fieldRef.Name)
                return field;
        }

        return null;
    }

    private static bool IsCorLib(AssemblyDefinition assembly)
    {
        var name = assembly.Name;
        return name is not null && (
            name.Contains("mscorlib") ||
            name.Contains("System.Runtime") ||
            name.Contains("System.Private.CoreLib") ||
            name.Contains("netstandard")
        );
    }

    private AssemblyDefinition? GetCorLibAssembly()
    {
        // Try to resolve mscorlib directly via the assembly resolver
        var mscorlibRef = new AssemblyReference("mscorlib", new Version(4, 0, 0, 0));
        return ResolveAssembly(mscorlibRef);
    }
}
