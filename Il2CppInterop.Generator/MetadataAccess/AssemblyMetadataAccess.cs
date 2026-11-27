using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.MetadataAccess;

public class AssemblyMetadataAccess : IIl2CppMetadataAccess
{
    private readonly List<AssemblyDefinition> myAssemblies = new();
    private readonly Dictionary<string, AssemblyDefinition> myAssembliesByName = new();
    private readonly Dictionary<(string AssemblyName, string TypeName), TypeDefinition> myTypesByName = new();

    public AssemblyMetadataAccess(IEnumerable<string> assemblyPaths)
    {
        Load(assemblyPaths.Select(path => AssemblyDefinition.FromFile(path, createRuntimeContext: false)));
    }

    public AssemblyMetadataAccess(IEnumerable<AssemblyDefinition> assemblies)
    {
        Load(assemblies);
    }

    public void Dispose()
    {
        myAssemblies.Clear();
        myAssembliesByName.Clear();
        myTypesByName.Clear();
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
        var sourceAssemblies = assemblies.ToList();
        if (sourceAssemblies.Count == 0)
            return;

        var existingContexts = sourceAssemblies.Select(assembly => assembly.RuntimeContext)
            .OfType<RuntimeContext>().Distinct().ToArray();
        if (existingContexts.Length > 1)
            throw new ArgumentException("Source assemblies must belong to a single runtime context or be loaded with createRuntimeContext: false.", nameof(assemblies));

        var corlib = sourceAssemblies.FirstOrDefault(assembly =>
            assembly.Name == "mscorlib" || assembly.Name == "System.Private.CoreLib");
        var targetRuntime = (corlib ?? sourceAssemblies[0]).ManifestModule!.OriginalTargetRuntime;
        var runtimeContext = existingContexts.FirstOrDefault() ??
            new RuntimeContext(targetRuntime, new Il2CppAssemblyResolver(), corLibReference: corlib);

        foreach (var sourceAssembly in sourceAssemblies)
        {
            if (sourceAssembly.RuntimeContext is null)
                runtimeContext.AddAssembly(sourceAssembly);
            myAssemblies.Add(sourceAssembly);
            myAssembliesByName[sourceAssembly.Name!] = sourceAssembly;
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
