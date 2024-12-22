using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.Extensions;

public static class CustomAttributeEx
{
    public static long ExtractOffset(this IHasCustomAttribute originalMethod)
    {
        return ExtractLong(originalMethod, "AddressAttribute", "Offset");
    }

    public static long ExtractRva(this IHasCustomAttribute originalMethod)
    {
        return ExtractLong(originalMethod, "AddressAttribute", "RVA");
    }

    public static long ExtractToken(this IHasCustomAttribute originalMethod)
    {
        return ExtractLong(originalMethod, "TokenAttribute", "Token");
    }

    public static int ExtractFieldOffset(this IHasCustomAttribute originalField)
    {
        return ExtractInt(originalField, "FieldOffsetAttribute", "Offset");
    }

    public static string? GetElementAsString(this CustomAttributeArgument argument)
    {
        return argument.Element as Utf8String ?? argument.Element as string;
    }

    private static string? Extract(this IHasCustomAttribute originalMethod, string attributeName,
        string parameterName)
    {
        var attribute = originalMethod.CustomAttributes.SingleOrDefault(it => it.Constructor?.DeclaringType?.Name == attributeName);
        var field = attribute?.Signature?.NamedArguments.SingleOrDefault(it => it.MemberName == parameterName);

        return field?.Argument.GetElementAsString();
    }

    private static long ExtractLong(this IHasCustomAttribute originalMethod, string attributeName, string parameterName)
    {
        return Convert.ToInt64(Extract(originalMethod, attributeName, parameterName), 16);
    }

    private static int ExtractInt(this IHasCustomAttribute originalMethod, string attributeName, string parameterName)
    {
        return Convert.ToInt32(Extract(originalMethod, attributeName, parameterName), 16);
    }
}
