using Microsoft.Extensions.Logging;

namespace Il2CppInterop.StructGenerator;

internal static class Program
{
    internal static void Main(string[] args)
    {
        // Directory that contains il2cpp headers. Directory must contain .h files named with their unity version.
        // https://github.com/nneonneo/Il2CppVersions/tree/master/headers
        var headersDirectory = args[0];

        // Directory to write managed struct wrapper sources to
        var outputDirectory = args[1];

        Il2CppStructWrapperGeneratorOptions options = new(headersDirectory, outputDirectory, new ConsoleLogger());
        Il2CppStructWrapperGenerator.Generate(options);
    }

    private sealed class ConsoleLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {formatter.Invoke(state, exception)}");
        }
    }
}
