using Il2CppInterop.Common.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Il2CppInterop.Common;

public static class LoggerExtensions
{
    public static T AddLogger<T>(this T host, ILogger logger) where T : BaseHost
    {
        host.AddComponent(new Logger(logger));
        return host;
    }
}

internal class Logger : IHostComponent
{
    public static ILogger Instance { get; private set; } = NullLogger.Instance;

    public Logger(ILogger logger)
    {
        Instance = logger;
    }

    public void Start()
    {
    }

    public void Dispose()
    {
        Instance = NullLogger.Instance;
    }
}
