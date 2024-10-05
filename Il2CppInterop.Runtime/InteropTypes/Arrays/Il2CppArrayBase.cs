using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public abstract class Il2CppArrayBase : Il2CppObjectBase, IEnumerable
{
    protected Il2CppArrayBase(IntPtr pointer) : base(pointer)
    {
    }

    /// <summary>
    /// The pointer to the first element in the array.
    /// </summary>
    private protected unsafe IntPtr ArrayStartPointer => IntPtr.Add(Pointer, sizeof(Il2CppObject) /* base */ + sizeof(void*) /* bounds */ + sizeof(nuint) /* max_length */);

    public int Length => (int)IL2CPP.il2cpp_array_length(Pointer);

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
}
public abstract class Il2CppArrayBase<T> : Il2CppArrayBase, IList<T>, IReadOnlyList<T>
{
    protected Il2CppArrayBase(IntPtr pointer) : base(pointer)
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
        if (array == null) throw new ArgumentNullException(nameof(array));
        if (arrayIndex < 0) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < Length)
            throw new ArgumentException(
                $"Not enough space in target array: need {Length} slots, have {array.Length - arrayIndex}");

        for (var i = 0; i < Length; i++)
            array[i + arrayIndex] = this[i];
    }

    bool ICollection<T>.Remove(T item)
    {
        return ThrowImmutableLength();
    }

    public new int Length => base.Length;// For binary compatibility
    public int Count => Length;
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

    protected static void StaticCtorBody(Type ownType)
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
        if (pointer == IntPtr.Zero) return null;

        if (typeof(T) == typeof(string))
            return new Il2CppStringArray(pointer) as Il2CppArrayBase<T>;
        if (typeof(T).IsValueType) // can't construct required types here directly because of unfulfilled generic constraint
            return Activator.CreateInstance(typeof(Il2CppStructArray<>).MakeGenericType(typeof(T)), pointer) as
                Il2CppArrayBase<T>;
        if (typeof(Il2CppObjectBase).IsAssignableFrom(typeof(T)))
            return Activator.CreateInstance(typeof(Il2CppReferenceArray<>).MakeGenericType(typeof(T)), pointer) as
                Il2CppArrayBase<T>;

        throw new ArgumentException(
            $"{typeof(T)} is not a value type, not a string and not an IL2CPP object; it can't be used in IL2CPP arrays");
    }

    private class IndexEnumerator : IEnumerator<T>
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
            return ++myIndex < myArray.Count;
        }

        public void Reset()
        {
            myIndex = -1;
        }

        object? IEnumerator.Current => Current;
        public T Current => myArray[myIndex];
    }
}
