using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

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

    public static string MakeValidInSource(this string str)
    {
        if (string.IsNullOrEmpty(str))
            return "";

        char[]? chars = null;
        for (var i = 0; i < str.Length; i++)
        {
            var it = str[i];
            if (IsValidInSource(it))
                continue;

            chars ??= str.ToCharArray();
            chars[i] = '_';
        }

        var result = chars is null ? str : new string(chars);
        return char.IsDigit(result[0]) ? "_" + result : result;
    }

    private static bool IsValidInSource(char c) => char.IsDigit(c) || (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z') || c == '_' || c == '`';

    public static Utf8String MakeValidInSource(this Utf8String? str)
    {
        if (Utf8String.IsNullOrEmpty(str))
            return Utf8String.Empty;

        ReadOnlySpan<byte> data = str.GetBytesUnsafe();

        var length = data.Length;
        byte[]? rentedArray = null;
        Span<byte> rentedArraySpan = default;

        if (char.IsDigit((char)data[0]))
        {
            length++;
            rentedArray = ArrayPool<byte>.Shared.Rent(length);
            rentedArray[0] = (byte)'_';
            rentedArraySpan = rentedArray.AsSpan(1);
            data.CopyTo(rentedArraySpan);
        }

        for (var i = 0; i < data.Length; i++)
        {
            if (IsValidInSource((char)data[i]))
                continue;

            if (rentedArray is null)
            {
                rentedArray = ArrayPool<byte>.Shared.Rent(length);
                rentedArraySpan = rentedArray.AsSpan();
                data.CopyTo(rentedArraySpan);
            }
            rentedArraySpan[i] = (byte)'_';
        }

        if (rentedArray is not null)
        {
            var result = new Utf8String(rentedArray, 0, length);
            ArrayPool<byte>.Shared.Return(rentedArray);
            return result;
        }
        else
        {
            return str;
        }
    }

    public static bool IsInvalidInSource([NotNullWhen(true)] this string? str)
    {
        if (str is null)
            return false;

        for (var i = 0; i < str.Length; i++)
        {
            var it = str[i];
            if (!char.IsDigit(it) && !((it >= 'a' && it <= 'z') || (it >= 'A' && it <= 'Z')) && it != '_' &&
                it != '`') return true;
        }

        return false;
    }

    public static bool IsInvalidInSource([NotNullWhen(true)] this Utf8String? str)
    {
        return IsInvalidInSource(str?.Value);
    }

    public static bool IsObfuscated([NotNullWhen(true)] this string? str, GeneratorOptions options)
    {
        if (str is null)
            return false;
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

    public static bool StartsWith([NotNullWhen(true)] this Utf8String? str, string value)
    {
        return str is not null && str.Value.StartsWith(value, StringComparison.Ordinal);
    }

    /// <summary>
    /// Construct an unmangled name for a type signature.
    /// </summary>
    /// <param name="typeRef"></param>
    /// <param name="declaringType">The declaring type to use for resolving generic type parameter names.</param>
    /// <param name="declaringMethod">The declaring method to use for resolving generic method parameter names.</param>
    /// <returns></returns>
    public static string GetUnmangledName(this TypeSignature typeRef, TypeDefinition? declaringType = null, MethodDefinition? declaringMethod = null)
    {
        var builder = new StringBuilder();
        if (typeRef is GenericInstanceTypeSignature genericInstance)
        {
            builder.Append(genericInstance.GenericType.ToTypeSignature().GetUnmangledName(declaringType, declaringMethod));
            foreach (var genericArgument in genericInstance.TypeArguments)
            {
                builder.Append("_");
                builder.Append(genericArgument.GetUnmangledName(declaringType, declaringMethod));
            }
        }
        else if (typeRef is ByReferenceTypeSignature byRef)
        {
            builder.Append("byref_");
            builder.Append(byRef.BaseType.GetUnmangledName(declaringType, declaringMethod));
        }
        else if (typeRef is PointerTypeSignature pointer)
        {
            builder.Append("ptr_");
            builder.Append(pointer.BaseType.GetUnmangledName(declaringType, declaringMethod));
        }
        else if (typeRef is GenericParameterSignature genericParameter)
        {
            if (genericParameter.ParameterType == GenericParameterType.Type)
                builder.Append(declaringType!.GenericParameters[genericParameter.Index].Name.MakeValidInSource());
            else
                builder.Append(declaringMethod!.GenericParameters[genericParameter.Index].Name.MakeValidInSource());
        }
        else
        {
            if (typeRef.Namespace == "Il2CppInterop.Runtime" && (typeRef.Name?.StartsWith("Il2Cpp") ?? false) &&
                typeRef.Name.Contains("Array"))
                builder.Append("ArrayOf");
            else
                builder.Append(typeRef.Name?.Replace('`', '_'));
        }

        return builder.ToString();
    }
}
