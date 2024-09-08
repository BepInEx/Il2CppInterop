using AsmResolver.DotNet;

namespace Il2CppInterop;

internal static class Utils
{
    public static List<AssemblyDefinition> LoadAssembliesFrom(DirectoryInfo directoryInfo)
    {
        var inputAssemblies = directoryInfo.EnumerateFiles("*.dll").Select(f => AssemblyDefinition.FromFile(
            f.FullName)).ToList();

        return inputAssemblies;
    }
}
