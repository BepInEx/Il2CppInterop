using System;
using System.Buffers;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public abstract class Il2CppMultiArrayBase : Il2CppSystem.Array
{
    /// <summary>
    /// The pointer to the first element in the array.
    /// </summary>
    private protected unsafe IntPtr ArrayStartPointer => IntPtr.Add(Pointer, sizeof(Il2CppObject) /* base */ + sizeof(void*) /* bounds */ + sizeof(nuint) /* max_length */);

    private protected Il2CppMultiArrayBase(ObjectPointer pointer) : base(pointer)
    {
    }

    private protected static unsafe ObjectPointer AllocateArray(ReadOnlySpan<int> lengths, IntPtr arrayClass)
    {
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
        where TArray : Il2CppMultiArrayBase<TElement>
        where TElement : IIl2CppType<TElement>
    {
        Il2CppClassPointerStore<TArray>.NativeClassPtr = IL2CPP.il2cpp_array_class_get(Il2CppClassPointerStore<TElement>.NativeClassPtr, rank);
    }
}
public abstract class Il2CppMultiArrayBase<T> : Il2CppMultiArrayBase where T : IIl2CppType<T>
{
    private protected Il2CppMultiArrayBase(ObjectPointer pointer) : base(pointer)
    {
    }

    private protected Il2CppMultiArrayBase(ReadOnlySpan<int> lengths, IntPtr arrayClass) : this(AllocateArray(lengths, arrayClass))
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
