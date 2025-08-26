namespace Il2CppInterop.Runtime.InteropTypes;

public readonly record struct ObjectPointer(IntPtr Value)
{
    public static explicit operator ObjectPointer(IntPtr value) => new(value);
    public static explicit operator IntPtr(ObjectPointer value) => value.Value;
}
