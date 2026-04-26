using System;
using Il2CppInterop.Runtime.Injection;

namespace Il2CppInterop.Runtime.Startup;

public record RuntimeConfiguration
{
    public required Version UnityVersion { get; init; }
    public required IDetourProvider DetourProvider { get; init; }
}
