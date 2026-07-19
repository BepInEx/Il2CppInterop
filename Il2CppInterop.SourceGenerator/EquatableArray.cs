using System.Collections;

namespace Il2CppInterop.SourceGenerator;

internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IReadOnlyList<T>
    where T : IEquatable<T>
{
    private readonly IReadOnlyList<T> array;
    public EquatableArray(IReadOnlyList<T> array)
    {
        this.array = array;
    }

    public T this[int index] => array[index];

    public int Count => array.Count;

    public bool Equals(EquatableArray<T> other)
    {
        if (array.Count != other.array.Count)
        {
            return false;
        }
        for (var i = array.Count - 1; i >= 0; i--)
        {
            if (!array[i].Equals(other.array[i]))
            {
                return false;
            }
        }
        return true;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return array.GetEnumerator();
    }

    public override int GetHashCode() => array.Count;

    IEnumerator IEnumerable.GetEnumerator()
    {
        return array.GetEnumerator();
    }
}
