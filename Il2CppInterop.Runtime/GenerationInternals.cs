using System.ComponentModel;
using Il2CppInterop.Common;

namespace Il2CppInterop.Runtime;

/// <summary>
/// Do not reference this class. Everything in it is an implementation detail of the generator. Breaking changes may occur at any time without warning.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class GenerationInternals
{
    public static unsafe string? Il2CppStringToManaged(Il2CppSystem.String? il2CppString)
    {
        if (il2CppString == null)
            return null;

        var il2CppStringPtr = il2CppString.Pointer;

        var length = IL2CPP.il2cpp_string_length(il2CppStringPtr);
        var chars = IL2CPP.il2cpp_string_chars(il2CppStringPtr);

        return new string(chars, 0, length);
    }

    public static unsafe Il2CppSystem.String? ManagedStringToIl2Cpp(string? str)
    {
        if (str == null)
            return null;

        fixed (char* chars = str)
        {
            return new Il2CppSystem.String((ObjectPointer)IL2CPP.il2cpp_string_new_utf16(chars, str.Length));
        }
    }

    public static nint Il2CppGCHandleGetTargetOrThrow(nint gchandle)
    {
        var obj = IL2CPP.il2cpp_gchandle_get_target(gchandle);
        if (obj == nint.Zero)
            throw new ObjectCollectedException("Object was garbage collected in IL2CPP domain");
        return obj;
    }

    public static bool Il2CppGCHandleGetTargetWasCollected(nint gchandle)
    {
        var obj = IL2CPP.il2cpp_gchandle_get_target(gchandle);
        return obj == nint.Zero;
    }
}
