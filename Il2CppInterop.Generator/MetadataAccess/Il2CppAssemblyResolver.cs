using System.Collections.Concurrent;
using AsmResolver.DotNet;
using AsmResolver.IO;

namespace Il2CppInterop.Generator.MetadataAccess;

/// <summary>
/// Custom assembly resolver for IL2CPP/HybridCLR environments.
/// Handles signature mismatches between hotfix DLLs and AOT DLLs by ignoring public key token comparison.
/// </summary>
internal sealed class Il2CppAssemblyResolver : AssemblyResolverBase
{
    // Cache for fast assembly lookup by name (ignoring signature)
    private readonly ConcurrentDictionary<string, AssemblyDefinition> _assemblyCache = new();

    public Il2CppAssemblyResolver() : base(new ByteArrayFileService())
    {
    }

    protected override string? ProbeRuntimeDirectories(AssemblyDescriptor assembly) => null;

    public void AddToCache(AssemblyDefinition assembly)
    {
        // Add to base cache with full signature
        AddToCache(assembly, assembly);

        // Also add to our name-only cache for signature-ignoring lookups
        if (!string.IsNullOrEmpty(assembly.Name))
        {
            _assemblyCache.TryAdd(assembly.Name!, assembly);
        }
    }

    /// <summary>
    /// Resolves an assembly, ignoring public key token mismatches.
    /// This is necessary because IL2CPP remaps DLL signatures differently than the original assemblies.
    /// Uses 'new' to hide base method since AssemblyResolverBase.Resolve is not virtual.
    /// </summary>
    public new AssemblyDefinition? Resolve(AssemblyDescriptor assembly)
    {
        // First try the standard resolution
        var result = base.Resolve(assembly);
        if (result != null)
            return result;

        // If standard resolution failed, try name-only lookup (ignoring signature)
        if (!string.IsNullOrEmpty(assembly.Name) && _assemblyCache.TryGetValue(assembly.Name!, out var cached))
        {
            return cached;
        }

        return null;
    }
}
