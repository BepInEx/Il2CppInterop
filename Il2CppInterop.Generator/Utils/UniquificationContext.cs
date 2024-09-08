using Il2CppInterop.Generator.Extensions;

namespace Il2CppInterop.Generator.Utils;

public class UniquificationContext
{
    private readonly GeneratorOptions myGeneratorOptions;
    private readonly SortedSet<(string, float)> myPrefixes = new(new Item2Comparer());
    private readonly Dictionary<string, int> myUniquifiersCount = new();

    public UniquificationContext(GeneratorOptions generatorOptions)
    {
        myGeneratorOptions = generatorOptions;
    }

    public bool CheckFull()
    {
        return myUniquifiersCount.Count >= myGeneratorOptions.TypeDeobfuscationMaxUniquifiers;
    }

    public void Push(string str, bool noSubstring = false)
    {
        if (str.IsInvalidInSource()) return;

        var stringPrefix = noSubstring
            ? str
            : SubstringBounded(str, 0, myGeneratorOptions.TypeDeobfuscationCharsPerUniquifier);
        var currentCount = myUniquifiersCount[stringPrefix] = myUniquifiersCount.GetOrCreate(stringPrefix, _ => 0) + 1;
        myPrefixes.Add((stringPrefix, myUniquifiersCount.Count + currentCount * 2 + myPrefixes.Count / 100f));
    }

    public void Push(List<string> strings, bool noSubstring = false)
    {
        foreach (var str in strings)
            Push(str, noSubstring);
    }

    public string GetTop()
    {
        return string.Join("",
            myPrefixes.Take(myGeneratorOptions.TypeDeobfuscationMaxUniquifiers).Select(it => it.Item1));
    }

    private static string SubstringBounded(string str, int startIndex, int length)
    {
        length = Math.Min(length, str.Length);
        return str.Substring(startIndex, length);
    }

    private class Item2Comparer : IComparer<(string, float)>
    {
        public int Compare((string, float) x, (string, float) y)
        {
            return x.Item2.CompareTo(y.Item2);
        }
    }
}
