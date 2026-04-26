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

    public static void EmitLdarg(this ILGenerator il, int index)
    {
        switch (index)
        {
            case 0:
                il.Emit(OpCodes.Ldarg_0);
                break;
            case 1:
                il.Emit(OpCodes.Ldarg_1);
                break;
            case 2:
                il.Emit(OpCodes.Ldarg_2);
                break;
            case 3:
                il.Emit(OpCodes.Ldarg_3);
                break;
            default:
                if (index <= byte.MaxValue)
                {
                    il.Emit(OpCodes.Ldarg_S, (byte)index);
                }
                else
                {
                    il.Emit(OpCodes.Ldarg, index);
                }
                break;
        }
    }

    public static void EmitStarg(this ILGenerator il, int index)
    {
        switch (index)
        {
            case 0:
                il.Emit(OpCodes.Starg_S, (byte)0);
                break;
            case 1:
                il.Emit(OpCodes.Starg_S, (byte)1);
                break;
            case 2:
                il.Emit(OpCodes.Starg_S, (byte)2);
                break;
            case 3:
                il.Emit(OpCodes.Starg_S, (byte)3);
                break;
            default:
                if (index <= byte.MaxValue)
                {
                    il.Emit(OpCodes.Starg_S, (byte)index);
                }
                else
                {
                    il.Emit(OpCodes.Starg, index);
                }
                break;
        }
    }
}
