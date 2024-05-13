using System.Text;
using AsmResolver;
using AsmResolver.DotNet.Signatures.Types;

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

    public static bool NameShouldBePrefixed(this Utf8String? str, GeneratorOptions options)
    {
        return NameShouldBePrefixed(str?.Value ?? "", options);
    }

    public static string UnSystemify(this string str, GeneratorOptions options)
    {
        const string Il2CppPrefix = "Il2Cpp";
        return str.NameShouldBePrefixed(options) ? Il2CppPrefix + str : str;
    }

    public static string UnSystemify(this Utf8String? str, GeneratorOptions options)
    {
        return UnSystemify(str?.Value ?? "", options);
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

    public static string FilterInvalidInSourceChars(this Utf8String? str)
    {
        return str?.Value.FilterInvalidInSourceChars() ?? "";
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

    public static bool IsInvalidInSource(this Utf8String? str)
    {
        return IsInvalidInSource(str?.Value ?? "");
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

    public static bool IsObfuscated(this Utf8String? str, GeneratorOptions options)
    {
        return IsObfuscated(str?.Value ?? "", options);
    }

    public static ulong StableHash(this string str)
    {
        ulong hash = 0;
        for (var i = 0; i < str.Length; i++)
            hash = hash * 37 + str[i];

        return hash;
    }

    public static ulong StableHash(this Utf8String? str)
    {
        return StableHash(str?.Value ?? "");
    }

    public static string GetUnmangledName(this TypeSignature typeRef)
    {
        var builder = new StringBuilder();
        if (typeRef is GenericInstanceTypeSignature genericInstance)
        {
            builder.Append(genericInstance.GenericType.ToTypeSignature().GetUnmangledName());
            foreach (var genericArgument in genericInstance.TypeArguments)
            {
                builder.Append("_");
                builder.Append(genericArgument.GetUnmangledName());
            }
        }
        else if (typeRef is ByReferenceTypeSignature byRef)
        {
            builder.Append("byref_");
            builder.Append(byRef.BaseType.GetUnmangledName());
        }
        else if (typeRef is PointerTypeSignature pointer)
        {
            builder.Append("ptr_");
            builder.Append(pointer.BaseType.GetUnmangledName());
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
