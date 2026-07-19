using Il2CppInterop.Common.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Il2CppInterop.Common;

public static class Logger
{
    public static ILogger Instance { get; private set; } = NullLogger.Instance;

    public static T AddLogger<T>(this T host, ILogger logger) where T : BaseHost
    {
        host.AddComponent(new LoggerComponent(logger));
        return host;
    }

    private sealed class LoggerComponent : IHostComponent
    {
        public LoggerComponent(ILogger logger)
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
}
