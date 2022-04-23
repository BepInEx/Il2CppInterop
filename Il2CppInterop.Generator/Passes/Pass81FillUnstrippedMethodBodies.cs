using System.Collections.Generic;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Mono.Cecil;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator.Passes
{
    public static class Pass81FillUnstrippedMethodBodies
    {
        private static readonly
            List<(MethodDefinition unityMethod, MethodDefinition newMethod, TypeRewriteContext processedType, AssemblyKnownImports imports)> StuffToProcess =
                new List<(MethodDefinition unityMethod, MethodDefinition newMethod, TypeRewriteContext processedType, AssemblyKnownImports imports)>();
        
        public static void DoPass(RewriteGlobalContext context)
        {
            int methodsSucceeded = 0;
            int methodsFailed = 0;
            
            foreach (var (unityMethod, newMethod, processedType, imports) in StuffToProcess)
            {
                var success = UnstripTranslator.TranslateMethod(unityMethod, newMethod, processedType, imports);
                if (success == false)
                {
                    methodsFailed++;
                    UnstripTranslator.ReplaceBodyWithException(newMethod, imports);
                }
                else
                    methodsSucceeded++;
            }
            
            Logger.Info(""); // finish progress line
            Logger.Info($"IL unstrip statistics: {methodsSucceeded} successful, {methodsFailed} failed");
        }

        public static void PushMethod(MethodDefinition unityMethod, MethodDefinition newMethod, TypeRewriteContext processedType, AssemblyKnownImports imports)
        {
            StuffToProcess.Add((unityMethod, newMethod, processedType, imports));
        }
    }
}