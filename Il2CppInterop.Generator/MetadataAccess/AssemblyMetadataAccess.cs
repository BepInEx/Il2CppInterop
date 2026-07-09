using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.MetadataAccess;

public class AssemblyMetadataAccess : IIl2CppMetadataAccess
{
    private readonly Il2CppAssemblyResolver myAssemblyResolver = new();
    private readonly List<AssemblyDefinition> myAssemblies = new();
    private readonly Dictionary<string, AssemblyDefinition> myAssembliesByName = new();
    private readonly Dictionary<(string AssemblyName, string TypeName), TypeDefinition> myTypesByName = new();
    private readonly bool myIsHybridCLREnvironment;

    public AssemblyMetadataAccess(IEnumerable<string> assemblyPaths, bool isHybridCLREnvironment = false)
    {
        myIsHybridCLREnvironment = isHybridCLREnvironment;
        Load(assemblyPaths.Select(AssemblyDefinition.FromFile));
    }

    public AssemblyMetadataAccess(IEnumerable<AssemblyDefinition> assemblies, bool isHybridCLREnvironment = false)
    {
        myIsHybridCLREnvironment = isHybridCLREnvironment;
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

    /// <summary>
    /// Adds external assemblies to the internal resolver cache so that type references
    /// in source assemblies can resolve against them (e.g., reference interop assemblies).
    /// </summary>
    public void AddReferenceAssemblies(IEnumerable<AssemblyDefinition> assemblies)
    {
        foreach (var assembly in assemblies)
            myAssemblyResolver.AddToCache(assembly);
    }

    private void Load(IEnumerable<AssemblyDefinition> assemblies)
    {
        foreach (var sourceAssembly in assemblies)
        {
            myAssemblies.Add(sourceAssembly);
            myAssembliesByName[sourceAssembly.Name!] = sourceAssembly;
            sourceAssembly.ManifestModule!.MetadataResolver = myIsHybridCLREnvironment
                ? new HybridCLRMetadataResolver(myAssemblyResolver)
                : new DefaultMetadataResolver(myAssemblyResolver);
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
