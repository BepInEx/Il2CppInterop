using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public abstract class Il2CppArrayBase : Il2CppSystem.Array
{
    private protected Il2CppArrayBase(ObjectPointer pointer) : base(pointer)
    {
    }

    /// <summary>
    /// The pointer to the first element in the array.
    /// </summary>
    private protected unsafe IntPtr ArrayStartPointer => IntPtr.Add(Pointer, sizeof(Il2CppObject) /* base */ + sizeof(void*) /* bounds */ + sizeof(nuint) /* max_length */);

    public new int Length => base.Length;

    private protected static bool ThrowImmutableLength()
    {
        throw new NotSupportedException("Arrays have immutable length");
    }

    private protected void ThrowIfIndexOutOfRange(int index)
    {
        if ((uint)index >= (uint)Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                "Array index may not be negative or above length of the array");
    }

    public T LoadElementUnsafe<T>(int index) where T : unmanaged
    {
        ThrowIfIndexOutOfRange(index);
        return Unsafe.As<byte, T>(ref GetUnsafeSpanForElement(index)[0]);
    }

    public void StoreElementUnsafe<T>(int index, T value) where T : unmanaged
    {
        ThrowIfIndexOutOfRange(index);
        ref var element = ref Unsafe.As<byte, T>(ref GetUnsafeSpanForElement(index)[0]);
        element = value;
    }

    private protected virtual Span<byte> GetUnsafeSpanForElement(int index)
    {
        throw new NotSupportedException("Only rank 1 arrays support unsafe element access");
    }

    private protected static unsafe ObjectPointer AllocateArray(ReadOnlySpan<int> lengths, IntPtr arrayClass)
    {
        if (lengths.Length <= 1)
        {
            throw new ArgumentException("Use single-dimensional array allocation for single-dimensional arrays.", nameof(lengths));
        }

        var sizes = ArrayPool<ulong>.Shared.Rent(lengths.Length);
        for (var i = 0; i < lengths.Length; i++)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(lengths[i]);
            sizes[i] = (ulong)lengths[i];
        }

        var lowerBounds = ArrayPool<ulong>.Shared.Rent(lengths.Length);
        lowerBounds.AsSpan().Clear();

        ObjectPointer result;
        fixed (ulong* pSizes = sizes)
        {
            fixed (ulong* pLowerBounds = lowerBounds)
            {
                result = (ObjectPointer)IL2CPP.il2cpp_array_new_full(arrayClass, pSizes, pLowerBounds);
            }
        }

        ArrayPool<ulong>.Shared.Return(sizes);
        ArrayPool<ulong>.Shared.Return(lowerBounds);

        return result;
    }

    // https://github.com/js6pak/libil2cpp-archive/blob/90c6b7ed1c291d54b257d751a4d743d07dea8d62/vm/Array.cpp#L273-L286
    private protected long IndexFromIndices(ReadOnlySpan<int> indices)
    {
        int rank = GetRank();
        long pos;

        pos = indices[0] - GetLowerBound(0);

        for (var i = 1; i < rank; i++)
            pos = pos * GetLength(i) + indices[i] - GetLowerBound(i);

        return pos;
    }

    private protected static void SetClassPointer<TArray, TElement>(uint rank)
        where TArray : Il2CppArrayBase
        where TElement : IIl2CppType<TElement>
    {
        Il2CppClassPointerStore<TArray>.NativeClassPtr = IL2CPP.il2cpp_array_class_get(Il2CppClassPointerStore<TElement>.NativeClassPtr, rank);
    }
}
public abstract class Il2CppArrayBase<T> : Il2CppArrayBase
    where T : IIl2CppType<T>
{
    private protected Il2CppArrayBase(ObjectPointer pointer) : base(pointer)
    {
    }

    private protected Il2CppArrayBase(ReadOnlySpan<int> lengths, IntPtr arrayClass) : this(AllocateArray(lengths, arrayClass))
    {
    }

    public T this[params ReadOnlySpan<int> indices]
    {
        get => GetElementAddress(indices).GetValue()!;
        set => GetElementAddress(indices).SetValue(value);
    }

    public unsafe ByReference<T> GetElementAddress(params ReadOnlySpan<int> indices)
    {
        var flatIndex = IndexFromIndices(indices);
        void* elementPtr = (byte*)ArrayStartPointer + flatIndex * T.Size;
        return new ByReference<T>(elementPtr);
    }
}
