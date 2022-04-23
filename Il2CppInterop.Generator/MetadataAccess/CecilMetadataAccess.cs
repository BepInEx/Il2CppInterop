using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace Il2CppInterop.Generator.MetadataAccess
{
    public class CecilMetadataAccess : IIl2CppMetadataAccess
    {
        private readonly Resolver myAssemblyResolver = new();
        private readonly List<AssemblyDefinition> myAssemblies = new();
        private readonly Dictionary<string, AssemblyDefinition> myAssembliesByName = new();
        private readonly Dictionary<(string AssemblyName, string TypeName), TypeDefinition> myTypesByName = new();
        
        public CecilMetadataAccess(IEnumerable<string> assemblyPaths)
        {
            var metadataResolver = new MetadataResolver(myAssemblyResolver);

            Load(assemblyPaths.Select(path => AssemblyDefinition.ReadAssembly(path, new ReaderParameters(ReadingMode.Deferred) { MetadataResolver = metadataResolver })));
        }

        public CecilMetadataAccess(IEnumerable<AssemblyDefinition> assemblies)
        {
            // Note: At the moment this assumes that passed assemblies have their own assembly resolver set up
            // If this is not true, this can cause issues with reference resolving
            Load(assemblies);
        }

        private void Load(IEnumerable<AssemblyDefinition> assemblies)
        {
            foreach (var sourceAssembly in assemblies)
            {
                myAssemblyResolver.Register(sourceAssembly);
                myAssemblies.Add(sourceAssembly);
                myAssembliesByName[sourceAssembly.Name.Name] = sourceAssembly;
            }
            
            foreach (var sourceAssembly in myAssemblies)
            {
                var sourceAssemblyName = sourceAssembly.Name.Name;
                foreach (var type in sourceAssembly.MainModule.Types)
                {
                    // todo: nested types?
                    myTypesByName[(sourceAssemblyName, type.FullName)] = type;
                }
            }
        }

        public void Dispose()
        {
            foreach (var assemblyDefinition in myAssemblies) 
                assemblyDefinition.Dispose();
            
            myAssemblies.Clear();
            myAssembliesByName.Clear();
            myAssemblyResolver.Dispose();
        }

        public AssemblyDefinition? GetAssemblyBySimpleName(string name) => myAssembliesByName.TryGetValue(name, out var result) ? result : null;

        public TypeDefinition? GetTypeByName(string assemblyName, string typeName) => myTypesByName.TryGetValue((assemblyName, typeName), out var result) ? result : null;

        public IList<AssemblyDefinition> Assemblies => myAssemblies;

        public IList<GenericInstanceType>? GetKnownInstantiationsFor(TypeDefinition genericDeclaration) => null;
        public string? GetStringStoredAtAddress(long offsetInMemory) => null;
        public MethodReference? GetMethodRefStoredAt(long offsetInMemory) => null;
        
        private class Resolver : DefaultAssemblyResolver
        {
            public void Register(AssemblyDefinition ass) => RegisterAssembly(ass);
        }
    }
}