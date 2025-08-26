using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public abstract class Il2CppArrayBase : Il2CppSystem.Array, IEnumerable, ICollection
{
    private protected Il2CppArrayBase(IntPtr pointer) : base(pointer)
    {
    }
    private protected Il2CppArrayBase(ObjectPointer pointer) : base((nint)pointer)
    {
    }

    /// <summary>
    /// The pointer to the first element in the array.
    /// </summary>
    private protected unsafe IntPtr ArrayStartPointer => IntPtr.Add(Pointer, sizeof(Il2CppObject) /* base */ + sizeof(void*) /* bounds */ + sizeof(nuint) /* max_length */);

    public int Length => (int)IL2CPP.il2cpp_array_length(Pointer);

    int ICollection.Count => Length;

    bool ICollection.IsSynchronized => false;

    object ICollection.SyncRoot => throw new NotSupportedException();

    public abstract IEnumerator GetEnumerator();

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

    private protected virtual Span<byte> GetUnsafeSpanForElement(int index) => throw new NotSupportedException("This array type does not support unsafe element access");

    private protected abstract void CopyTo(Array array, int index);

    void ICollection.CopyTo(Array array, int index) => CopyTo(array, index);

    private static Il2CppUnmanagedArray<byte> Get()
    {
        Set(0, 1, 2);
        return [0, 1, 2];
    }

    private static void Set(params Il2CppUnmanagedArray<byte> arr)
    {
    }

    public static Il2CppUnmanagedArray<T> CreateUnmanaged<T>(ReadOnlySpan<T> arr) where T : unmanaged
    {
        return new Il2CppUnmanagedArray<T>(arr);
    }
}
public abstract class Il2CppArrayBase<T> : Il2CppArrayBase, IList<T>, IReadOnlyList<T>
{
    private protected Il2CppArrayBase(IntPtr pointer) : base(pointer)
    {
    }
    private protected Il2CppArrayBase(ObjectPointer pointer) : base(pointer)
    {
    }

    public sealed override IEnumerator<T> GetEnumerator()
    {
        return new IndexEnumerator(this);
    }

    void ICollection<T>.Add(T item)
    {
        ThrowImmutableLength();
    }

    void ICollection<T>.Clear()
    {
        ThrowImmutableLength();
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

    private protected sealed override void CopyTo(Array array, int index) => CopyTo((T[])array, index);

    bool ICollection<T>.Remove(T item)
    {
        return ThrowImmutableLength();
    }

    int ICollection<T>.Count => Length;
    int IReadOnlyCollection<T>.Count => Length;
    bool ICollection<T>.IsReadOnly => false;

    public int IndexOf(T item)
    {
        for (var i = 0; i < Length; i++)
            if (Equals(item, this[i]))
                return i;

        return -1;
    }

    void IList<T>.Insert(int index, T item)
    {
        ThrowImmutableLength();
    }

    void IList<T>.RemoveAt(int index)
    {
        ThrowImmutableLength();
    }

    public abstract T this[int index] { get; set; }

    private protected static void StaticCtorBody(Type ownType)
    {
        var nativeClassPtr = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (nativeClassPtr == IntPtr.Zero)
            return;

        var targetClassType = IL2CPP.il2cpp_array_class_get(nativeClassPtr, 1);
        if (targetClassType == IntPtr.Zero)
            return;

        Il2CppClassPointerStore.SetNativeClassPointer(ownType, targetClassType);
        Il2CppClassPointerStore.SetNativeClassPointer(typeof(Il2CppArrayBase<T>), targetClassType);
        Il2CppClassPointerStore<Il2CppArrayBase<T>>.CreatedTypeRedirect = ownType;
    }

    [return: NotNullIfNotNull(nameof(il2CppArray))]
    public static implicit operator T[]?(Il2CppArrayBase<T>? il2CppArray)
    {
        if (il2CppArray == null)
            return null;

        var arr = new T[il2CppArray.Length];
        for (var i = 0; i < arr.Length; i++)
            arr[i] = il2CppArray[i];

        return arr;
    }

    public static Il2CppArrayBase<T>? WrapNativeGenericArrayPointer(IntPtr pointer)
    {
        if (pointer == IntPtr.Zero)
            return null;

        if (typeof(T) == typeof(string))
            return new Il2CppStringArray(pointer) as Il2CppArrayBase<T>;
        if (typeof(T).IsValueType) // can't construct required types here directly because of unfulfilled generic constraint
            return Activator.CreateInstance(typeof(Il2CppStructArray<>).MakeGenericType(typeof(T)), pointer) as Il2CppArrayBase<T>;
        if (typeof(Il2CppObjectBase).IsAssignableFrom(typeof(T)))
            return Activator.CreateInstance(typeof(Il2CppReferenceArray<>).MakeGenericType(typeof(T)), pointer) as Il2CppArrayBase<T>;

        throw new ArgumentException(
            $"{typeof(T)} is not a value type, not a string and not an IL2CPP object; it can't be used in IL2CPP arrays");
    }

    private sealed class IndexEnumerator : IEnumerator<T>
    {
        private Il2CppArrayBase<T> myArray;
        private int myIndex = -1;

        public IndexEnumerator(Il2CppArrayBase<T> array)
        {
            myArray = array;
        }

        public void Dispose()
        {
            myArray = null!;
        }

        public bool MoveNext()
        {
            return ++myIndex < ((ICollection<T>)myArray).Count;
        }

        public void Reset()
        {
            myIndex = -1;
        }

        object? IEnumerator.Current => Current;
        public T Current => myArray[myIndex];
    }
}
