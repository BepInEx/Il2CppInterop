using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;
using AsmResolver.IO;

namespace Il2CppInterop.Generator.MetadataAccess;

internal sealed class Il2CppAssemblyResolver : AssemblyResolverBase
{
    public Il2CppAssemblyResolver() : base(new ModuleReaderParameters(new ByteArrayFileService()))
    {
    }

    public override string? ProbeAssemblyFilePath(AssemblyDescriptor assembly, ModuleDefinition? originModule) =>
        ProbeSearchDirectories(assembly, originModule);
}
