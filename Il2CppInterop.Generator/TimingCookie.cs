using System;
using System.Diagnostics;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Generator
{
    internal readonly struct TimingCookie : IDisposable
    {
        private readonly Stopwatch myStopwatch;
        public TimingCookie(string message)
        {
            Logger.Info(message + "... ");
            myStopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            Logger.Info($"Done in {myStopwatch.Elapsed}");
        }
    }
}