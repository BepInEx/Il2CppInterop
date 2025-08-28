namespace Il2CppSystem;

public struct IntPtr
{
    public static implicit operator nint(IntPtr value) => throw null;
    public static implicit operator IntPtr(nint value) => throw null;
}
