using AsmResolver.DotNet;
using Il2CppInterop.Common.Maps;

#nullable enable

namespace Il2CppInterop.Pdb.Generator;

public class MethodAddressToTokenMap : MethodAddressToTokenMapBase<AssemblyDefinition, MethodDefinition>
{
    public MethodAddressToTokenMap(string filePath) : base(filePath)
    {
    }

    protected override AssemblyDefinition? LoadAssembly(string assemblyName)
    {
        var filesDirt = Path.GetDirectoryName(myFilePath)!;
        assemblyName = assemblyName.Substring(0, assemblyName.IndexOf(','));
        return AssemblyDefinition.FromFile(Path.Combine(filesDirt, assemblyName + ".dll"));
    }

    protected override MethodDefinition? ResolveMethod(AssemblyDefinition? assembly, int token)
    {
        if (assembly?.ManifestModule?.TryLookupMember(token, out MethodDefinition? result) ?? false)
        {
            return result;
        }
        return null;
    }
}
