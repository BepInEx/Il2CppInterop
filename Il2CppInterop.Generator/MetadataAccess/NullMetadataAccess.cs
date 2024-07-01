using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.MetadataAccess;

public class NullMetadataAccess : IMetadataAccess
{
    public static readonly NullMetadataAccess Instance = new();

    public void Dispose()
    {
    }

    public IList<AssemblyDefinition> Assemblies => Array.Empty<AssemblyDefinition>();

    public AssemblyDefinition? GetAssemblyBySimpleName(string name)
    {
        return null;
    }

    public TypeDefinition? GetTypeByName(string assemblyName, string typeName)
    {
        return null;
    }

    public IList<GenericInstanceTypeSignature>? GetKnownInstantiationsFor(TypeReference genericDeclaration)
    {
        return null;
    }

    public string? GetStringStoredAtAddress(long offsetInMemory)
    {
        return null;
    }

    public MemberReference? GetMethodRefStoredAt(long offsetInMemory)
    {
        return null;
    }
}
