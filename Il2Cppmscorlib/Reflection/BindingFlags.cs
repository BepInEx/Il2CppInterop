namespace Il2CppSystem.Reflection;

public struct BindingFlags
{
    private readonly int value__;
    public static readonly BindingFlags Default = (BindingFlags)0;
    public static readonly BindingFlags IgnoreCase = (BindingFlags)1;
    public static readonly BindingFlags DeclaredOnly = (BindingFlags)2;
    public static readonly BindingFlags Instance = (BindingFlags)4;
    public static readonly BindingFlags Static = (BindingFlags)8;
    public static readonly BindingFlags Public = (BindingFlags)0x10;
    public static readonly BindingFlags NonPublic = (BindingFlags)0x20;
    public static readonly BindingFlags FlattenHierarchy = (BindingFlags)0x40;
    public static readonly BindingFlags InvokeMethod = (BindingFlags)0x100;
    public static readonly BindingFlags CreateInstance = (BindingFlags)0x200;
    public static readonly BindingFlags GetField = (BindingFlags)0x400;
    public static readonly BindingFlags SetField = (BindingFlags)0x800;
    public static readonly BindingFlags GetProperty = (BindingFlags)0x1000;
    public static readonly BindingFlags SetProperty = (BindingFlags)0x2000;
    public static readonly BindingFlags PutDispProperty = (BindingFlags)0x4000;
    public static readonly BindingFlags PutRefDispProperty = (BindingFlags)0x8000;
    public static readonly BindingFlags ExactBinding = (BindingFlags)0x10000;
    public static readonly BindingFlags SuppressChangeType = (BindingFlags)0x20000;
    public static readonly BindingFlags OptionalParamBinding = (BindingFlags)0x40000;
    public static readonly BindingFlags IgnoreReturn = (BindingFlags)0x1000000;
    public static readonly BindingFlags DoNotWrapExceptions = (BindingFlags)0x2000000;

    public static explicit operator BindingFlags(int value) => throw null;

    public static BindingFlags operator &(BindingFlags left, BindingFlags right) => throw null;
    public static BindingFlags operator |(BindingFlags left, BindingFlags right) => throw null;
    public static BindingFlags operator ^(BindingFlags left, BindingFlags right) => throw null;
    public static BindingFlags operator ~(BindingFlags value) => throw null;
}
