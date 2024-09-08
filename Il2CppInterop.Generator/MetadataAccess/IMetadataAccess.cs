using AsmResolver.DotNet;

namespace Il2CppInterop.Generator.MetadataAccess;

public interface IMetadataAccess : IDisposable
{
    IList<AssemblyDefinition> Assemblies { get; }

    AssemblyDefinition? GetAssemblyBySimpleName(string name);
    TypeDefinition? GetTypeByName(string assemblyName, string typeName);
}
