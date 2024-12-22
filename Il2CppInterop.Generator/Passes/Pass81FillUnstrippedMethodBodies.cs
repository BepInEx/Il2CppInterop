using AsmResolver.DotNet;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator.Passes;

public static class Pass81FillUnstrippedMethodBodies
{
    private static readonly
        List<(MethodDefinition unityMethod, MethodDefinition newMethod, TypeRewriteContext processedType,
            RuntimeAssemblyReferences imports)> StuffToProcess =
            new();

    public static void DoPass(RewriteGlobalContext context)
    {
        var methodsSucceeded = 0;
        var methodsFailed = 0;

        foreach (var (unityMethod, newMethod, processedType, imports) in StuffToProcess)
        {
            var success = UnstripTranslator.TranslateMethod(unityMethod, newMethod, processedType, imports);
            if (success == false)
            {
                methodsFailed++;
                UnstripTranslator.ReplaceBodyWithException(newMethod, imports);
            }
            else
            {
                methodsSucceeded++;
            }
        }

        StuffToProcess.Clear();
        StuffToProcess.Capacity = 0;

        Logger.Instance.LogInformation("IL unstrip statistics: {MethodsSucceeded} successful, {MethodsFailed} failed", methodsSucceeded,
            methodsFailed);
    }

    public static void PushMethod(MethodDefinition unityMethod, MethodDefinition newMethod,
        TypeRewriteContext processedType, RuntimeAssemblyReferences imports)
    {
        StuffToProcess.Add((unityMethod, newMethod, processedType, imports));
    }
}
