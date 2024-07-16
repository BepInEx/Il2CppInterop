using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

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
        {
            foreach (var typeContext in assemblyContext.Types)
            {
                foreach (var methodContext in typeContext.Methods)
                {
                    methodContext.CtorPhase2();

                    if (Pass15GenerateMemberContexts.HasObfuscatedMethods)
                    {
                        var callerCount = 0;
                        if (Pass16ScanMethodRefs.MapOfCallers.TryGetValue(methodContext.Rva, out var callers))
                            callerCount = callers.Count;

                        methodContext.NewMethod.CustomAttributes.Add(
                            new CustomAttribute((ICustomAttributeType)assemblyContext.Imports.CallerCountAttributector.Value, new CustomAttributeSignature(new CustomAttributeArgument(assemblyContext.Imports.Module.Int(), callerCount))));

                        if (!Pass15GenerateMemberContexts.HasObfuscatedMethods) continue;
                        if (methodContext.UnmangledName?.Contains("_PDM_") is not true) continue;
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
            }
        }

        if (Pass15GenerateMemberContexts.HasObfuscatedMethods)
        {
            Logger.Instance.LogTrace("Dead method statistics: 0t={Top0Caller} mt={TopNZCaller} 0n={Nested0Caller} mn={NestedNZCaller}", pdmTop0Caller, pdmTopNZCaller, pdmNested0Caller, pdmNestedNZCaller);
        }
    }
}
