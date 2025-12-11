using System.Reflection;
using System.Reflection.Emit;

namespace Il2CppInterop.HarmonySupport;

internal static class Extensions
{
    public static void Emit(this ILGenerator il, OpCode opCode, MethodBase methodBase)
    {
        if (methodBase is MethodInfo methodInfo)
        {
            il.Emit(opCode, methodInfo);
        }
        else if (methodBase is ConstructorInfo constructorInfo)
        {
            il.Emit(opCode, constructorInfo);
        }
        else
        {
            throw new InvalidOperationException("Can't emit a call to a MethodBase that isn't either a MethodInfo or a ConstructorInfo");
        }
    }
}
