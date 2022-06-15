using Mono.Cecil;

namespace Il2CppInterop;

internal static class Utils
{
    public static List<AssemblyDefinition> LoadAssembliesFrom(DirectoryInfo directoryInfo)
    {
        var resolver = new BasicResolver();
        var inputAssemblies = directoryInfo.EnumerateFiles("*.dll").Select(f => AssemblyDefinition.ReadAssembly(
            f.FullName,
            new ReaderParameters { AssemblyResolver = resolver })).ToList();
        foreach (var assembly in inputAssemblies)
        {
            resolver.Register(assembly);
        }

        return inputAssemblies;
    }

    private class BasicResolver : DefaultAssemblyResolver
    {
        public void Register(AssemblyDefinition ad) => RegisterAssembly(ad);
    }
}
