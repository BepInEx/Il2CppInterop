using Il2CppInterop.Generator.Contexts;
using Mono.Cecil;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator.Passes
{
    public static class Pass18FinalizeMethodContexts
    {
        public static int TotalPotentiallyDeadMethods;

        public static void DoPass(RewriteGlobalContext context)
        {
            var pdmNested0Caller = 0;
            var pdmNestedNZCaller = 0;
            var pdmTop0Caller = 0;
            var pdmTopNZCaller = 0;

            foreach (var assemblyContext in context.Assemblies)
                foreach (var typeContext in assemblyContext.Types)
                    foreach (var methodContext in typeContext.Methods)
                    {
                        methodContext.CtorPhase2();

                        if (Pass15GenerateMemberContexts.HasObfuscatedMethods)
                        {
                            int callerCount = 0;
                            if (Pass16ScanMethodRefs.MapOfCallers.TryGetValue(methodContext.Rva, out var callers))
                                callerCount = callers.Count;

                            methodContext.NewMethod.CustomAttributes.Add(
                                new CustomAttribute(assemblyContext.Imports.CallerCountAttributeCtor)
                                {
                                    ConstructorArguments =
                                        { new CustomAttributeArgument(assemblyContext.Imports.Int, callerCount) }
                                });

                            if (!Pass15GenerateMemberContexts.HasObfuscatedMethods) continue;
                            if (!methodContext.UnmangledName.Contains("_PDM_")) continue;
                            TotalPotentiallyDeadMethods++;

                            var hasZeroCallers = callerCount == 0;
                            if (methodContext.DeclaringType.OriginalType.IsNested)
                            {
                                if (hasZeroCallers)
                                    pdmNested0Caller++;
                                else
                                    pdmNestedNZCaller++;
                            }
                            else
                            {
                                if (hasZeroCallers)
                                    pdmTop0Caller++;
                                else
                                    pdmTopNZCaller++;
                            }
                        }
                    }

            if (Pass15GenerateMemberContexts.HasObfuscatedMethods)
            {
                Logger.Trace("");
                Logger.Trace($"Dead method statistics: 0t={pdmTop0Caller} mt={pdmTopNZCaller} 0n={pdmNested0Caller} mn={pdmNestedNZCaller}");
            }
        }
    }
}