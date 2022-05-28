#nullable enable
using System.Reflection;

namespace Il2CppInterop.Common.XrefScans;

internal static class GeneratedDatabasesUtil
{
    private static string? DatabasesLocationOverride => Environment.GetEnvironmentVariable("IL2CPP_INTEROP_DATABASES_LOCATION");

    public static string GetDatabasePath(string databaseName)
    {
        return Path.Combine(
            (DatabasesLocationOverride ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location))!,
            databaseName);
    }
}
