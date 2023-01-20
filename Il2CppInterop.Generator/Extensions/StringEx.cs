using System.Text;
using Mono.Cecil;

namespace Il2CppInterop.Generator.Extensions;

public static class StringEx
{
    public static bool NameShouldBePrefixed(this string str, GeneratorOptions options)
    {
        if (options.Il2CppPrefixMode == GeneratorOptions.PrefixMode.OptIn)
        {
            foreach (var prefix in options.NamespacesAndAssembliesToPrefix)
                if (str.StartsWith(prefix, StringComparison.Ordinal))
                    return true;
            return false;
        }
        else
        {
            foreach (var prefix in options.NamespacesAndAssembliesToNotPrefix)
                if (str.StartsWith(prefix, StringComparison.Ordinal))
                    return false;

            return true;
        }
    }
    public static string UnSystemify(this string str, GeneratorOptions options)
    {
        const string Il2CppPrefix = "Il2Cpp";
        return str.NameShouldBePrefixed(options) ? Il2CppPrefix + str : str;
    }

    public static string FilterInvalidInSourceChars(this string str)
    {
        var chars = str.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var it = chars[i];
            if (!char.IsDigit(it) && !((it >= 'a' && it <= 'z') || (it >= 'A' && it <= 'Z')) && it != '_' &&
                it != '`') chars[i] = '_';
        }

        return new string(chars);
    }

    public static bool IsInvalidInSource(this string str)
    {
        for (var i = 0; i < str.Length; i++)
        {
            var it = str[i];
            if (!char.IsDigit(it) && !((it >= 'a' && it <= 'z') || (it >= 'A' && it <= 'Z')) && it != '_' &&
                it != '`') return true;
        }

        return false;
    }

    public static bool IsObfuscated(this string str, GeneratorOptions options)
    {
        if (options.ObfuscatedNamesRegex != null)
            return options.ObfuscatedNamesRegex.IsMatch(str);

        foreach (var it in str)
            if (!char.IsDigit(it) && !((it >= 'a' && it <= 'z') || (it >= 'A' && it <= 'Z')) && it != '_' &&
                it != '`' && it != '.' && it != '<' && it != '>')
                return true;

        return false;
    }

    public static ulong StableHash(this string str)
    {
        ulong hash = 0;
        for (var i = 0; i < str.Length; i++)
            hash = hash * 37 + str[i];

        return hash;
    }

    public static string GetUnmangledName(this TypeReference typeRef)
    {
        var builder = new StringBuilder();
        if (typeRef is GenericInstanceType genericInstance)
        {
            builder.Append(genericInstance.ElementType.GetUnmangledName());
            foreach (var genericArgument in genericInstance.GenericArguments)
            {
                builder.Append("_");
                builder.Append(genericArgument.GetUnmangledName());
            }
        }
        else if (typeRef is ByReferenceType byRef)
        {
            builder.Append("byref_");
            builder.Append(byRef.ElementType.GetUnmangledName());
        }
        else if (typeRef is PointerType pointer)
        {
            builder.Append("ptr_");
            builder.Append(pointer.ElementType.GetUnmangledName());
        }
        else
        {
            if (typeRef.Namespace == "Il2CppInterop.Runtime" && typeRef.Name.StartsWith("Il2Cpp") &&
                typeRef.Name.Contains("Array"))
                builder.Append("ArrayOf");
            else
                builder.Append(typeRef.Name.Replace('`', '_'));
        }

        return builder.ToString();
    }
}
