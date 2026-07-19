namespace Il2CppInterop.StructGenerator.CodeGen;

internal enum ElementProtection
{
    Private,
    Protected,
    Internal,
    Public
}
internal static class ElementProtectionExtensions
{
    public static string ToCSharpString(this ElementProtection value) => value switch
    {
        ElementProtection.Private => "private",
        ElementProtection.Protected => "protected",
        ElementProtection.Internal => "internal",
        ElementProtection.Public => "public",
        _ => throw new InvalidOperationException($"Unknown ElementProtection value: {value}")
    };
}
