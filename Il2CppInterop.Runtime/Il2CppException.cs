using System;
using System.Diagnostics;
using System.Text;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime;

public interface IIl2CppException
{
    Exception CreateSystemException();
}
public class Il2CppException : Exception
{
    [ThreadStatic]
    private static byte[]? ourMessageBytes;

    public static Func<IntPtr, string>? ParseMessageHook;

    public readonly Il2CppSystem.Exception Il2cppObject;

    public Il2CppException(Il2CppSystem.Exception il2cppObject)
    {
        Il2cppObject = il2cppObject;
    }

    public override string Message => BuildMessage(Il2cppObject);

    private static unsafe string BuildMessage(Il2CppSystem.Exception il2cppException)
    {
        var exception = il2cppException.Pointer;

        if (ParseMessageHook != null)
            return ParseMessageHook(exception);

        ourMessageBytes ??= new byte[65536];
        fixed (byte* message = ourMessageBytes)
        {
            IL2CPP.il2cpp_format_exception(exception, message, ourMessageBytes.Length);
        }

        var builtMessage = Encoding.UTF8.GetString(ourMessageBytes, 0, Array.IndexOf(ourMessageBytes, (byte)0));
        return $"""
            {builtMessage}
            --- BEGIN IL2CPP STACK TRACE ---
            {il2cppException.ToString(false, true)}
            --- END IL2CPP STACK TRACE ---

            """;
    }

    public static void RaiseExceptionIfNecessary(IntPtr returnedException)
    {
        if (returnedException == IntPtr.Zero)
            return;

        var il2cppException = (IIl2CppException?)Il2CppObjectPool.Get(returnedException);
        Debug.Assert(il2cppException is not null);

        throw il2cppException.CreateSystemException();
    }
}
