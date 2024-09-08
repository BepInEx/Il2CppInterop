namespace Il2CppInterop.Generator.Extensions;

public static class CollectionEx
{
    public static TV GetOrCreate<TK, TV>(this IDictionary<TK, TV> dict, TK key, Func<TK, TV> valueFactory)
        where TK : notnull
    {
        if (!dict.TryGetValue(key, out var result))
        {
            result = valueFactory(key);
            dict[key] = result;
        }

        return result;
    }

    public static void AddLocked<T>(this List<T> list, T value)
    {
        lock (list)
        {
            list.Add(value);
        }
    }
}
