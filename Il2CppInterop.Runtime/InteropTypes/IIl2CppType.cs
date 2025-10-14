using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public interface IIl2CppType
{
    IntPtr ObjectClass { get; }
}
public interface IIl2CppType<TSelf> : IIl2CppType where TSelf : notnull, IIl2CppType<TSelf>
{
    static abstract int Size { get; }
    static virtual string AssemblyName => typeof(TSelf).Assembly.GetName().Name ?? "";
    static virtual string Namespace => typeof(TSelf).Namespace ?? "";
    static virtual string Name => typeof(TSelf).Name;
    static abstract void WriteToSpan(TSelf? value, Span<byte> span);
    static abstract TSelf? ReadFromSpan(ReadOnlySpan<byte> span);
}
