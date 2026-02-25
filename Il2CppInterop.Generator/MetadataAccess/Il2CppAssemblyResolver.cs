using AsmResolver.DotNet;
using AsmResolver.IO;

namespace Il2CppInterop.Generator.MetadataAccess;

internal sealed class Il2CppAssemblyResolver : AssemblyResolverBase
{
    public Il2CppAssemblyResolver() : base(new ByteArrayFileService())
    {
    }

    protected override string? ProbeRuntimeDirectories(AssemblyDescriptor assembly) => null;

    public void AddToCache(AssemblyDefinition assembly)
    {
        AddToCache(assembly, assembly);
    }
}
