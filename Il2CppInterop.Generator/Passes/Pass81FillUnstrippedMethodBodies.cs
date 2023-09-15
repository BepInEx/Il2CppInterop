using System.Collections.Generic;
using System.IO.Compression;
using System.Text;
using Il2CppInterop.Common;
using Il2CppInterop.Generator.Contexts;
using Il2CppInterop.Generator.Utils;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

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
        var methodsFailed = new List<(MethodDefinition method, UnstripTranslator.Result result)>();

        foreach (var (unityMethod, newMethod, processedType, imports) in StuffToProcess)
        {
            var result = UnstripTranslator.TranslateMethod(unityMethod, newMethod, processedType, imports);
            if (result.IsError)
            {
                methodsFailed.Add((unityMethod, result));
                UnstripTranslator.ReplaceBodyWithException(newMethod, imports);
            }
            else
            {
                methodsSucceeded++;
            }
        }

        Logger.Instance.LogInformation("IL unstrip statistics: {MethodsSucceeded} successful, {MethodsFailed} failed", methodsSucceeded,
            methodsFailed.Count);

        SaveResults(context, methodsFailed);
    }

    private static void SaveResults(RewriteGlobalContext context,
        List<(MethodDefinition method, UnstripTranslator.Result result)> results)
    {
        var outPath = Path.GetDirectoryName(context.Options.OutputDir);
        outPath = Path.Combine(outPath, "unstrip.json.gz");
        outPath = Path.GetFullPath(outPath);
        var cwd = Directory.GetCurrentDirectory() + Path.DirectorySeparatorChar;
        if (!outPath.StartsWith(cwd))
            outPath = "unstrip.json.gz";
        else
            outPath = outPath.Substring(cwd.Length);
        Logger.Instance.LogInformation("Saving IL unstrip statistics to {OutPath}", outPath);

        results.Sort((left, right) => StringComparer.OrdinalIgnoreCase
            .Compare(left.method.MemberFullName(), right.method.MemberFullName()));
        using (var gzStream =
            new StreamWriter(
                new GZipStream(
                    File.Open(outPath, FileMode.Create),
                CompressionLevel.Fastest),
            new UTF8Encoding(false)))
        {
            using (var arr = SimpleJsonWriter.Create(gzStream).Array())
            {
                foreach (var (method, result) in results)
                {
                    using (var entry = arr.Object())
                    {
                        entry.Property("name")
                            .Value(method.MemberFullName());
                        entry.Property("fullName")
                            .Value(method.FullName);
                        entry.Property("scope")
                            .Value(method.DeclaringType.Scope.Name);
                        entry.Property("namespace")
                            .Value(method.DeclaringType.Namespace);
                        entry.Property("type")
                            .Value(method.DeclaringType.Name);
                        entry.Property("method")
                            .Value(method.Name);
                        var insProp = entry.Property("instruction");
                        if (result.offendingInstruction == null)
                            insProp.Value(null);
                        else
                            using (var ins = insProp.Object())
                            {
                                ins.Property("description")
                                    .Value(result.offendingInstruction.ToString());
                                ins.Property("opCode")
                                    .Value(result.offendingInstruction.OpCode.Name);
                                ins.Property("operandType")
                                    .Value(Enum.GetName(typeof(OperandType), result.offendingInstruction.OpCode.OperandType));
                                ins.Property("operandValueType")
                                    .Value(result.offendingInstruction.Operand?.GetType().Name);
                                ins.Property("operand")
                                    .Value(result.offendingInstruction.Operand?.ToString());
                            }
                        entry.Property("result")
                            .Value(Enum.GetName(typeof(UnstripTranslator.ErrorType), result.type));
                        entry.Property("reason")
                            .Value(result.reason);
                    }
                }
            }
        }
    }

    private static string MemberFullName(this MethodDefinition method)
    {
        if (method.DeclaringType == null)
            return method.Name;
        return $"{method.DeclaringType.FullName}::{method.Name}";
    }

    public static void PushMethod(MethodDefinition unityMethod, MethodDefinition newMethod,
        TypeRewriteContext processedType, RuntimeAssemblyReferences imports)
    {
        StuffToProcess.Add((unityMethod, newMethod, processedType, imports));
    }
}
