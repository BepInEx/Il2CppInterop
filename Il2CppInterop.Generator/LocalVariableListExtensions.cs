using Cpp2IL.Core.Model.Contexts;
using Il2CppInterop.Generator.Operands;

namespace Il2CppInterop.Generator;

internal static class LocalVariableListExtensions
{
    public static LocalVariable AddNew(this List<LocalVariable> localVariables, TypeAnalysisContext variableType)
    {
        var localVariable = new LocalVariable(variableType);
        localVariables.Add(localVariable);
        return localVariable;
    }
}
