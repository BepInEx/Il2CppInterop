namespace Il2CppInterop.Common;

public interface IIl2CppType
{
    IntPtr ObjectClass { get; }
}
public interface IIl2CppType<TSelf> : IIl2CppType where TSelf : notnull, IIl2CppType<TSelf>
{
    /// <summary>
    /// The native size of the type in bytes
    /// </summary>
    static abstract int Size { get; }
    /// <summary>
    /// The name of the assembly that the type is defined in
    /// </summary>
    static virtual string AssemblyName
    {
        get
        {
            var result = typeof(TSelf).Assembly.GetName().Name;
            return string.IsNullOrEmpty(result) ? "Assembly-CSharp.dll" : result;
        }
    }
    /// <summary>
    /// The namespace of type
    /// </summary>
    static virtual string Namespace => typeof(TSelf).Namespace ?? "";
    /// <summary>
    /// The class name of the type
    /// </summary>
    static virtual string Name => typeof(TSelf).Name;
    /// <summary>
    /// Writes the native representation of the value to the provided span. The span is required to be at least <see cref="Size"/> bytes long.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="span">The span to write the value to.</param>
    static abstract void WriteToSpan(TSelf? value, Span<byte> span);
    /// <summary>
    /// Reads the native representation of the value from the provided span. The span is required to be at least <see cref="Size"/> bytes long.
    /// </summary>
    /// <param name="span">The span to read the value from.</param>
    /// <returns>The value read from the span.</returns>
    static abstract TSelf? ReadFromSpan(ReadOnlySpan<byte> span);
}
