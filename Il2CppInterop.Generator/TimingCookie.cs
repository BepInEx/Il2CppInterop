using System.Diagnostics;
using Il2CppInterop.Common;
using Il2CppInterop.Generator;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator;

internal readonly struct TimingCookie : IDisposable
{
    private readonly Stopwatch myStopwatch;

    public TimingCookie(string message)
    {
        Logger.Instance.LogInformation("{Message}...", message);
        myStopwatch = Stopwatch.StartNew();
    }

    public void Dispose()
    {
        Logger.Instance.LogInformation("Done in {Elapsed}", myStopwatch.Elapsed);
    }
}
