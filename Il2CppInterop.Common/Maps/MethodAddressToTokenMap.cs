#nullable enable

using System.Reflection;

namespace Il2CppInterop.Common.Maps;

public class MethodAddressToTokenMap : MethodAddressToTokenMapBase<Assembly, MethodBase>
{
    public MethodAddressToTokenMap(string filePath) : base(filePath)
    {
    }

    protected override Assembly LoadAssembly(string assemblyName)
    {
        return Assembly.Load(assemblyName);
    }

    protected override MethodBase? ResolveMethod(Assembly? assembly, int token)
    {
        return assembly?.ManifestModule.ResolveMethod(token);
    }
}
