namespace Il2CppInterop.Common;

public interface IIl2CppType
{
    IntPtr ObjectClass { get; }
    /// <summary>
    /// Box this managed object as a native Il2Cpp object
    /// </summary>
    /// <remarks>
    /// For classes, this returns a pointer to the existing Il2Cpp object.<br/>
    /// For value types, this creates a new boxed Il2Cpp object.
    /// </remarks>
    /// <returns>The pointer to the boxed Il2Cpp object</returns>
    ObjectPointer BoxNative();
    /// <summary>
    /// Box this managed object
    /// </summary>
    /// <remarks>
    /// This method is necessary for semantically accurate boxing of Il2CppSystem.Nullable&lt;T&gt; in unstripped code.<br/>
    /// All other types are expected to use the default implementation.
    /// </remarks>
    /// <returns>The boxed object</returns>
    object Box() => this;
}
public interface IIl2CppType<TSelf> : IIl2CppType where TSelf : notnull, IIl2CppType<TSelf>
{
    /// <summary>
    /// The native size of the type in bytes
    /// </summary>
    static virtual int Size
    {
        get
        {
            if (typeof(TSelf).IsValueType)
            {
                throw new NotImplementedException($"{typeof(TSelf).FullName} did not implement this property.");
            }
            else
            {
                // All reference types are the size of a pointer in native code
                return IntPtr.Size;
            }
        }
    }
    /// <summary>
    /// Writes the native representation of the value to the provided span. The span is required to be at least <see cref="Size"/> bytes long.
    /// </summary>
    /// <param name="value">The value to write.</param>
    /// <param name="span">The span to write the value to.</param>
    static virtual void WriteToSpan(TSelf? value, Span<byte> span)
    {
        if (typeof(TSelf).IsValueType)
        {
            throw new NotImplementedException($"{typeof(TSelf).FullName} did not implement this method.");
        }
        else
        {
            Il2CppType.WriteReference(value, span);
        }
    }
    /// <summary>
    /// Reads the native representation of the value from the provided span. The span is required to be at least <see cref="Size"/> bytes long.
    /// </summary>
    /// <param name="span">The span to read the value from.</param>
    /// <returns>The value read from the span.</returns>
    static virtual TSelf? ReadFromSpan(ReadOnlySpan<byte> span)
    {
        if (typeof(TSelf).IsValueType)
        {
            throw new NotImplementedException($"{typeof(TSelf).FullName} did not implement this method.");
        }
        else
        {
            return Il2CppType.ReadReference<TSelf>(span);
        }
    }
    /// <summary>
    /// Unboxes the provided native Il2Cpp object to the type
    /// </summary>
    /// <remarks>
    /// This always returns <typeparamref name="TSelf"/>-typed objects, not subclasses of <typeparamref name="TSelf"/>.
    /// Callers are expected to know that this is the exact type of the object being unboxed.
    /// Otherwise, they should use <see cref="Il2CppObjectPool.Get(nint)"/> and <see cref="Unbox(object?)"/>.
    /// </remarks>
    /// <param name="pointer">The pointer to the native Il2Cpp object.</param>
    /// <returns>The unboxed value.</returns>
    /// <exception cref="NotSupportedException"><typeparamref name="TSelf"/> is an interface or abstract class.</exception>
    /// <exception cref="NotImplementedException"><typeparamref name="TSelf"/> did not implement this method.</exception>
    static virtual unsafe TSelf? UnboxNative(ObjectPointer pointer)
    {
        if (typeof(TSelf).IsValueType)
        {
            var unboxed = IL2CPP.il2cpp_object_unbox((nint)pointer);
            return TSelf.ReadFromSpan(new ReadOnlySpan<byte>((void*)unboxed, TSelf.Size));
        }
        else if (typeof(TSelf).IsInterface)
        {
            throw new NotSupportedException("Interfaces cannot be unboxed because they are not concrete types.");
        }
        else if (typeof(TSelf).IsAbstract)
        {
            throw new NotSupportedException("Abstract classes cannot be unboxed because they are not concrete types.");
        }
        else
        {
            throw new NotImplementedException($"{typeof(TSelf).FullName} did not implement this method.");
        }
    }
    /// <summary>
    /// Unboxes the provided object to the type
    /// </summary>
    /// <remarks>
    /// This is only intended to be overridden by Il2CppSystem.Nullable&lt;T&gt;, which has special unboxing behavior.
    /// </remarks>
    /// <param name="obj">The object to unbox.</param>
    /// <returns>The unboxed value.</returns>
    static virtual TSelf? Unbox(object? obj)
    {
        return (TSelf?)obj;
    }
}
