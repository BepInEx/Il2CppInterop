using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public interface IIl2CppValueType : IIl2CppType
{
    /// <summary>
    /// The native size of the type in bytes
    /// </summary>
    /// <remarks>
    /// This is not necessarily the same as <see cref="IIl2CppType{TSelf}.Size"/>.
    /// For example, it can differ when the object is boxed and the type argument is an interface.
    /// However, the implementation of this method simply returns the value provided by <see cref="IIl2CppType{TSelf}.Size"/> for the unboxed type.
    /// </remarks>
    int Size { get; }
    /// <summary>
    /// Writes the native representation of the value to the provided span. The span is required to be at least <see cref="Size"/> bytes long.
    /// </summary>
    /// <remarks>
    /// This is not necessarily the same as the method from <see cref="IIl2CppType{TSelf}"/>.
    /// For example, it can differ when the object is boxed and the type argument is an interface.
    /// However, the implementation of this method simply calls the method provided by <see cref="IIl2CppType{TSelf}"/> for the unboxed type.
    /// </remarks>
    /// <param name="span">The span to write the value to.</param>
    void WriteToSpan(Span<byte> span);
}
