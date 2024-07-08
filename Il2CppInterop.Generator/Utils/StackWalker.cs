using Il2CppInterop.Common;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Il2CppInterop.Generator.Utils;

internal static class StackWalker
{
    public static bool TryWalkStack(RuntimeAssemblyReferences imports,
        MethodDefinition target, int stackTarget,
        out TypeReference type)
    {
        return TryWalkStack(imports, target, stackTarget, out type, out var _);
    }

    public static bool TryWalkStack(RuntimeAssemblyReferences imports,
        MethodDefinition target, int stackTarget,
        out TypeReference type, out Instruction source)
    {
        if (TryWalkStack(imports, target,
            target.Body.Instructions.Count - 1,
            new[] { stackTarget }, out var results))
        {
            type = results[0].type;
            source = results[0].source;
            return true;
        }
        type = null;
        source = null;
        return false;
    }

    public static bool TryWalkStack(RuntimeAssemblyReferences imports,
        MethodDefinition target, IEnumerable<int> stackTargets,
        out (TypeReference type, Instruction source, int index)[] results)
    {
        return TryWalkStack(imports, target,
            target.Body.Instructions.Count - 1,
            stackTargets, out results);
    }

    /// <summary>
    ///     Walks the instructions of <paramref name="target"/> in reverse order
    ///     starting from <paramref name="startInstruction"/>.<br/>
    ///     The state of the stack is recreated and the <see cref="TypeReference"/>s
    ///     at the positions described by <paramref name="stackTargets"/> is returned
    ///     along with the instructions responsible.
    /// </summary>
    /// <param name="imports"></param>
    /// <param name="target">Method to walk</param>
    /// <param name="startInstruction">Instruction index of where to start walking</param>
    /// <param name="stackTargets">
    ///     Stack positions to resolve<br/>
    ///     Note that stackTarget 0 is the last argument to a method, not the first (nor <see langword="this"/>)
    /// </param>
    /// <param name="results">Resolved <see cref="TypeReference"/>s and their source instructions</param>
    /// <returns><see langword="true"/> if all <paramref name="stackTargets"/> where found, <see langword="false"/> otherwise</returns>
    /// <exception cref="NotSupportedException"></exception>
    public static bool TryWalkStack(RuntimeAssemblyReferences imports,
        MethodDefinition target, int startInstruction,
        IEnumerable<int> stackTargets, out (TypeReference type, Instruction source, int index)[] results)
    {
        var _stackTargets = new List<int>(stackTargets);
        _stackTargets.Sort();

        results = new (TypeReference type, Instruction source, int index)[_stackTargets.Count];
        for (var i = 0; i < _stackTargets.Count; i++)
            results[i].index = _stackTargets[i];

        var stackPos = 0;
        var stackTargetIdx = 0;
        var stackTarget = _stackTargets[0];

        for (var i = startInstruction; i >= 0; i--)
        {
            var ins = target.Body.Instructions[i];
            if (ins.OpCode.BreaksFlow())
            {
                // TODO follow branches
                // Without branch logic there's a possibility that we walk the stack incorrectly
                // causing the found TypeReference to be bogus
                return false;
            }

            var nPush = ins.PushAmount();
            if (stackPos == stackTarget && nPush > 0)
            {
                results[stackTargetIdx].source ??= ins;
                var code = ins.OpCode.Code;
                if (code == Code.Dup ||
                    code.IsLdind())
                {
                    stackPos = 0;
                    for (var j = stackTargetIdx; j < _stackTargets.Count; j++)
                        _stackTargets[j] -= stackTarget;
                    stackTarget = 0;
                    continue;
                }
                else if (code == Code.Call ||
                    code == Code.Callvirt)
                    results[stackTargetIdx].type = ((MethodReference)ins.Operand).ReturnType;
                else if (code == Code.Newobj)
                    results[stackTargetIdx].type = ((MethodReference)ins.Operand).DeclaringType;
                else if (code == Code.Ldfld ||
                    code == Code.Ldsfld)
                    results[stackTargetIdx].type = ((FieldReference)ins.Operand).FieldType;
                else if (ins.TryGetLdlocIndex(out var varArgIdx))
                {
                    var varArg = target.Body.Variables[varArgIdx];
                    results[stackTargetIdx].type = varArg.VariableType;
                }
                else if (ins.TryGetLdargIndex(target.HasThis, out var paramArgIdx))
                {
                    var paramArg = paramArgIdx switch
                    {
                        -1 => target.Body.ThisParameter,
                        _ => target.Parameters[paramArgIdx],
                    };
                    results[stackTargetIdx].type = paramArg.ParameterType;
                }
                else if (code == Code.Ldstr)
                    results[stackTargetIdx].type = imports.Module.String();
                else
                    return false;

                if (++stackTargetIdx < _stackTargets.Count)
                    stackTarget = _stackTargets[stackTargetIdx];
                else
                    return true;
            }

            var nPop = ins.PopAmount();
            stackPos += nPush - nPop;
        }
        return false;
    }
}

