namespace Il2CppInterop.Generator.Extensions;

internal static class HashSetExtensions
{
    public static void AddRange<T>(this HashSet<T> set, IEnumerable<T> values)
    {
        foreach (var value in values)
        {
            set.Add(value);
        }
    }
}
