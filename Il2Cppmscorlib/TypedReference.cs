namespace Il2CppSystem;

public struct TypedReference
{
    /// <summary>
    /// A field that Unity has, but is not present in .NET Framework nor .NET Core.
    /// </summary>
    /// <remarks>
    /// This field was added in <see href="https://github.com/Unity-Technologies/mono/commit/969a1d9f735c60b629b4488935908572d30e78f1"/>
    /// </remarks>
    public RuntimeTypeHandle type;

    /// <summary>
    /// A pointer to the data
    /// </summary>
    public IntPtr Value;

    /// <summary>
    /// The type handle
    /// </summary>
    public IntPtr Type;
}
