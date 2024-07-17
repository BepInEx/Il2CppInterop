using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.MetadataAccess;

public class AssemblyMetadataAccess : IIl2CppMetadataAccess
{
    private readonly Il2CppAssemblyResolver myAssemblyResolver = new();
    private readonly List<AssemblyDefinition> myAssemblies = new();
    private readonly Dictionary<string, AssemblyDefinition> myAssembliesByName = new();
    private readonly Dictionary<(string AssemblyName, string TypeName), TypeDefinition> myTypesByName = new();

    public AssemblyMetadataAccess(IEnumerable<string> assemblyPaths)
    {
        Load(assemblyPaths.Select(AssemblyDefinition.FromFile));
    }

    public AssemblyMetadataAccess(IEnumerable<AssemblyDefinition> assemblies)
    {
        // Note: At the moment this assumes that passed assemblies have their own assembly resolver set up
        // If this is not true, this can cause issues with reference resolving
        Load(assemblies);
    }

    public void Dispose()
    {
        myAssemblyResolver.ClearCache();
        myAssemblies.Clear();
        myAssembliesByName.Clear();
    }

    public AssemblyDefinition? GetAssemblyBySimpleName(string name)
    {
        return myAssembliesByName.TryGetValue(name, out var result) ? result : null;
    }

    public TypeDefinition? GetTypeByName(string assemblyName, string typeName)
    {
        return myTypesByName.TryGetValue((assemblyName, typeName), out var result) ? result : null;
    }

    public IList<AssemblyDefinition> Assemblies => myAssemblies;

    public IList<GenericInstanceTypeSignature>? GetKnownInstantiationsFor(TypeDefinition genericDeclaration)
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

    private void Load(IEnumerable<AssemblyDefinition> assemblies)
    {
        foreach (var sourceAssembly in assemblies)
        {
            myAssemblies.Add(sourceAssembly);
            myAssembliesByName[sourceAssembly.Name!] = sourceAssembly;
            sourceAssembly.ManifestModule!.MetadataResolver = new DefaultMetadataResolver(myAssemblyResolver);
            myAssemblyResolver.AddToCache(sourceAssembly);
        }

        foreach (var sourceAssembly in myAssemblies)
        {
            var sourceAssemblyName = sourceAssembly.Name!;
            foreach (var type in sourceAssembly.ManifestModule!.TopLevelTypes)
                // todo: nested types?
                myTypesByName[(sourceAssemblyName, type.FullName)] = type;
        }
    }
}
