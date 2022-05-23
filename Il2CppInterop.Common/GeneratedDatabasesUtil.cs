using System.Reflection;

namespace Il2CppInterop.Common;

public static class GeneratedDatabasesUtil
{
    public static string? DatabasesLocationOverride { get; set; } = null;

    public static string GetDatabasePath(string databaseName)
    {
        return Path.Combine(
            (DatabasesLocationOverride ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))!,
            databaseName);
    }
}
