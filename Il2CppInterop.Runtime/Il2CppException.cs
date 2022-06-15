using System;
using System.Text;

namespace Il2CppInterop.Runtime;

public class Il2CppException : Exception
{
    [ThreadStatic] private static byte[] ourMessageBytes;

    public static Func<IntPtr, string> ParseMessageHook;

    public Il2CppException(IntPtr exception) : base(BuildMessage(exception))
    {
    }

    private static unsafe string BuildMessage(IntPtr exception)
    {
        if (ParseMessageHook != null) return ParseMessageHook(exception);
        ourMessageBytes ??= new byte[65536];
        fixed (byte* message = ourMessageBytes)
        {
            IL2CPP.il2cpp_format_exception(exception, message, ourMessageBytes.Length);
        }

        var builtMessage = Encoding.UTF8.GetString(ourMessageBytes, 0, Array.IndexOf(ourMessageBytes, (byte)0));
        Il2CppSystem.Exception il2cppException = new(exception);
        return builtMessage + "\n" +
               "--- BEGIN IL2CPP STACK TRACE ---\n" +
               $"{il2cppException.ToString(false, true)}\n" +
               "--- END IL2CPP STACK TRACE ---\n";
    }

    public static void RaiseExceptionIfNecessary(IntPtr returnedException)
    {
        if (returnedException == IntPtr.Zero) return;
        throw new Il2CppException(returnedException);
    }
}
