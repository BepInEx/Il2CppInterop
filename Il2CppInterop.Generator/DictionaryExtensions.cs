namespace Il2CppInterop.Generator;

internal static class DictionaryExtensions
{
    public static TValue? TryGetValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey? key)
        where TKey : class
        where TValue : class
    {
        return key is not null && dictionary.TryGetValue(key, out var value) ? value : null;
    }

    public static TValue? GetValue<TKey, TValue>(this Dictionary<TKey, TValue> dictionary, TKey? key)
        where TKey : class
        where TValue : class
    {
        return key is not null ? dictionary[key] : null;
    }
}
