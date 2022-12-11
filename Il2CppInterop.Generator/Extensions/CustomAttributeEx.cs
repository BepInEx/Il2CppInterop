using Mono.Cecil;

namespace Il2CppInterop.Generator.Extensions;

public static class CustomAttributeEx
{
    public static long ExtractOffset(this ICustomAttributeProvider originalMethod)
    {
        return ExtractLong(originalMethod, "AddressAttribute", "Offset");
    }

    public static long ExtractRva(this ICustomAttributeProvider originalMethod)
    {
        return ExtractLong(originalMethod, "AddressAttribute", "RVA");
    }

    public static long ExtractToken(this ICustomAttributeProvider originalMethod)
    {
        return ExtractLong(originalMethod, "TokenAttribute", "Token");
    }

    public static int ExtractFieldOffset(this ICustomAttributeProvider originalField)
    {
        return ExtractInt(originalField, "FieldOffsetAttribute", "Offset");
    }

    private static string Extract(this ICustomAttributeProvider originalMethod, string attributeName,
        string parameterName)
    {
        var attribute = originalMethod.CustomAttributes.SingleOrDefault(it => it.AttributeType.Name == attributeName);
        var field = attribute?.Fields.SingleOrDefault(it => it.Name == parameterName);

        if (field?.Name == null) return null;

        return (string)field.Value.Argument.Value;
    }

    private static long ExtractLong(this ICustomAttributeProvider originalMethod, string attributeName, string parameterName)
    {
        return Convert.ToInt64(Extract(originalMethod, attributeName, parameterName), 16);
    }

    private static int ExtractInt(this ICustomAttributeProvider originalMethod, string attributeName, string parameterName)
    {
        return Convert.ToInt32(Extract(originalMethod, attributeName, parameterName), 16);
    }
}
