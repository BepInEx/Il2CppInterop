using Il2CppInterop.Generator.Contexts;

namespace Il2CppInterop.Generator.Passes;

public static class Pass16GenerateMemberContexts
{
    public static bool HasObfuscatedMethods;

    public static void DoPass(RewriteGlobalContext context)
    {
        foreach (var assemblyContext in context.Assemblies)
            foreach (var typeContext in assemblyContext.Types)
                typeContext.AddMembers();
    }
}
