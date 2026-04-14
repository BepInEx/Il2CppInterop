using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public static class Il2CppArrayRank1
{
    public static Il2CppArrayRank1<T> Create<T>(ReadOnlySpan<T> span) where T : IIl2CppType<T>
    {
        return new Il2CppArrayRank1<T>(span);
    }

    public static Il2CppArrayRank1<T> CreateUnmanaged<T, U>(ReadOnlySpan<U> span)
        where T : unmanaged, IIl2CppType<T>
        where U : unmanaged
    {
        if (Unsafe.SizeOf<T>() != Unsafe.SizeOf<U>())
            throw new ArgumentException($"Cannot create an array of {typeof(T)} from a span of {typeof(U)}: sizes do not match");

        return Create(MemoryMarshal.Cast<U, T>(span));
    }
}
[CollectionBuilder(typeof(Il2CppArrayRank1), nameof(Il2CppArrayRank1.Create))]
public sealed class Il2CppArrayRank1<T> : Il2CppArrayBase<T>, IIl2CppType<Il2CppArrayRank1<T>>, IList<T>, IReadOnlyList<T>, IEnumerable, ICollection
    where T : IIl2CppType<T>
{
    static Il2CppArrayRank1()
    {
        SetClassPointer<Il2CppArrayRank1<T>, T>(1);
        Il2CppObjectPool.RegisterInitializer(Il2CppClassPointerStore<Il2CppArrayRank1<T>>.NativeClassPointer, static (ptr) => new Il2CppArrayRank1<T>(ptr));
    }

    public Il2CppArrayRank1(ObjectPointer pointer) : base(pointer)
    {
    }

    public Il2CppArrayRank1(int length) : base(AllocateArray(length))
    {
    }

    public Il2CppArrayRank1(ReadOnlySpan<T> values) : this(values.Length)
    {
        for (var i_0 = 0; i_0 < values.Length; i_0++)
        {
            this[i_0] = values[i_0];
        }
    }

    private static ObjectPointer AllocateArray(int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        var elementTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPointer;
        if (elementTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException($"{nameof(Il2CppArrayRank1<>)} requires an Il2Cpp type, which {typeof(T)} isn't");
        return (ObjectPointer)IL2CPP.il2cpp_array_new(elementTypeClassPointer, (ulong)length);
    }

    public T this[int index]
    {
        get
        {
            ThrowIfIndexOutOfRange(index);
            return T.ReadFromSpan(AsSpan().Slice(index * T.Size, T.Size))!;
        }
        set
        {
            ThrowIfIndexOutOfRange(index);
            T.WriteToSpan(value, AsSpan().Slice(index * T.Size, T.Size));
        }
    }

    public unsafe ByReference<T> GetElementAddress(int index)
    {
        ThrowIfIndexOutOfRange(index);
        return new ByReference<T>((byte*)ArrayStartPointer.ToPointer() + index * T.Size);
    }

    private unsafe Span<byte> AsSpan()
    {
        return new Span<byte>(ArrayStartPointer.ToPointer(), Length * T.Size);
    }

    private protected override Span<byte> GetUnsafeSpanForElement(int index)
    {
        return GetElementAddress(index).AsSpan();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return new IndexEnumerator(this);
    }

    public bool Contains(T item)
    {
        return IndexOf(item) != -1;
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);
        ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);
        if (array.Length - arrayIndex < Length)
            throw new ArgumentException(
                $"Not enough space in target array: need {Length} slots, have {array.Length - arrayIndex}");

        for (var i = 0; i < Length; i++)
            array[i + arrayIndex] = this[i];
    }

    public int IndexOf(T item)
    {
        for (var i = 0; i < Length; i++)
            if (Equals(item, this[i]))
                return i;

        return -1;
    }

    #region Conversions
    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator Il2CppArrayRank1<T>?(T[]? array) => array is null ? null : new(array);

    public static explicit operator Il2CppArrayRank1<T>?(ReadOnlySpan<T> span)
    {
        return new Il2CppArrayRank1<T>(span);
    }

    public static explicit operator Il2CppArrayRank1<T>?(Span<T> span)
    {
        return new Il2CppArrayRank1<T>(span);
    }

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator T[]?(Il2CppArrayRank1<T>? array)
    {
        if (array is null)
            return null;

        var length_0 = array.GetLength(0);
        var result = new T[length_0];
        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            result[i_0] = array[i_0];
        }
        return result;
    }

    public static explicit operator ReadOnlySpan<T>(Il2CppArrayRank1<T>? array)
    {
        return (ReadOnlySpan<T>)(T[]?)array;
    }

    public static explicit operator Span<T>(Il2CppArrayRank1<T>? array)
    {
        return (Span<T>)(T[]?)array;
    }
    #endregion

    #region IIl2CppType Implementation
    static int IIl2CppType<Il2CppArrayRank1<T>>.Size => IntPtr.Size;
    nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<Il2CppArrayRank2<T>>.NativeClassPointer;
    static void IIl2CppType<Il2CppArrayRank1<T>>.WriteToSpan(Il2CppArrayRank1<T>? value, Span<byte> span) => Il2CppTypeHelper.WriteReference(value, span);
    static Il2CppArrayRank1<T>? IIl2CppType<Il2CppArrayRank1<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => Il2CppTypeHelper.ReadReference<Il2CppArrayRank1<T>>(span);
    #endregion

    #region Collection Implementation
    int ICollection.Count => Length;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => throw new NotSupportedException();
    int ICollection<T>.Count => Length;
    int IReadOnlyCollection<T>.Count => Length;
    bool ICollection<T>.IsReadOnly => false;

    void ICollection<T>.Add(T item)
    {
        ThrowImmutableLength();
    }

    void ICollection<T>.Clear()
    {
        ThrowImmutableLength();
    }

    void ICollection.CopyTo(Array array, int index) => CopyTo((T[])array, index);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void IList<T>.Insert(int index, T item)
    {
        ThrowImmutableLength();
    }

    bool ICollection<T>.Remove(T item)
    {
        return ThrowImmutableLength();
    }

    void IList<T>.RemoveAt(int index)
    {
        ThrowImmutableLength();
    }

    private sealed class IndexEnumerator(Il2CppArrayRank1<T> array) : IEnumerator<T>
    {
        private int index = -1;

        public void Dispose() => array = null!;

        public bool MoveNext()
        {
            return ++index < ((ICollection<T>)array).Count;
        }

        public void Reset() => index = -1;

        object? IEnumerator.Current => Current;
        public T Current => array[index];
    }
    #endregion
}
