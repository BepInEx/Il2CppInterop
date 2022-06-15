using System.Collections.Generic;
using Mono.Cecil;
using Mono.Collections.Generic;

namespace Il2CppInterop.Generator.MetadataAccess;

public class NullMetadataAccess : IMetadataAccess
{
    public static readonly NullMetadataAccess Instance = new();

    public void Dispose()
    {
    }

    public IList<AssemblyDefinition> Assemblies => ReadOnlyCollection<AssemblyDefinition>.Empty;

    public AssemblyDefinition? GetAssemblyBySimpleName(string name)
    {
        return null;
    }

    public TypeDefinition? GetTypeByName(string assemblyName, string typeName)
    {
        return null;
    }

    public IList<GenericInstanceType>? GetKnownInstantiationsFor(TypeReference genericDeclaration)
    {
        return null;
    }

    public string? GetStringStoredAtAddress(long offsetInMemory)
    {
        return null;
    }

    public MethodReference? GetMethodRefStoredAt(long offsetInMemory)
    {
        return null;
    }
}
