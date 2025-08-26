using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

[CollectionBuilder(typeof(Il2CppArrayBase), nameof(CreateUnmanaged))]
public sealed class Il2CppUnmanagedArray<T> : Il2CppArrayBase<T> where T : unmanaged
{
    static Il2CppUnmanagedArray()
    {
        StaticCtorBody(typeof(Il2CppUnmanagedArray<T>));
    }

    public Il2CppUnmanagedArray(IntPtr nativeObject) : base(nativeObject)
    {
    }
    public Il2CppUnmanagedArray(ObjectPointer nativeObject) : base(nativeObject)
    {
    }

    public Il2CppUnmanagedArray(long size) : base(AllocateArray(size))
    {
    }

    public Il2CppUnmanagedArray(ReadOnlySpan<T> arr) : base(AllocateArray(arr.Length))
    {
        arr.CopyTo(this);
    }

    public override T this[int index]
    {
        get => AsSpan()[index];
        set => AsSpan()[index] = value;
    }

    public unsafe Span<T> AsSpan()
    {
        return new Span<T>(ArrayStartPointer.ToPointer(), Length);
    }

    private protected override Span<byte> GetUnsafeSpanForElement(int index)
    {
        return MemoryMarshal.AsBytes(AsSpan().Slice(index, 1));
    }

    [return: NotNullIfNotNull(nameof(arr))]
    public static implicit operator Il2CppUnmanagedArray<T>?(T[]? arr)
    {
        if (arr == null) return null;

        return new Il2CppUnmanagedArray<T>(arr);
    }

    public static implicit operator Span<T>(Il2CppUnmanagedArray<T>? il2CppArray)
    {
        return il2CppArray is not null ? il2CppArray.AsSpan() : default;
    }

    public static implicit operator ReadOnlySpan<T>(Il2CppUnmanagedArray<T>? il2CppArray)
    {
        return il2CppArray is not null ? il2CppArray.AsSpan() : default;
    }

    private static IntPtr AllocateArray(long size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Array size must not be negative");

        var elementTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (elementTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException(
                $"{nameof(Il2CppUnmanagedArray<T>)} requires an Il2Cpp type, which {typeof(T)} isn't");
        return IL2CPP.il2cpp_array_new(elementTypeClassPointer, (ulong)size);
    }
}
