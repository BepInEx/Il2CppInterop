using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Il2CppInterop.Common.Host;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.Runtime;
using Il2CppInterop.Runtime.XrefScans;

namespace Il2CppInterop.Runtime.Startup;

public record RuntimeConfiguration
{
    public Version UnityVersion { get; init; }
    public IDetourProvider DetourProvider { get; init; }
}

public sealed class Il2CppInteropRuntime : BaseHost
{
    private Il2CppInteropRuntime()
    {
    }

    public static Il2CppInteropRuntime Instance => GetInstance<Il2CppInteropRuntime>();

    public Version UnityVersion { get; private init; }

    public IDetourProvider DetourProvider { get; private init; }

    public static Il2CppInteropRuntime Create(RuntimeConfiguration configuration)
    {
        var res = new Il2CppInteropRuntime
        {
            UnityVersion = configuration.UnityVersion,
            DetourProvider = configuration.DetourProvider
        };
        SetInstance(res);
        res.AddXrefScanner<Il2CppInteropRuntime, XrefScanImpl>();
        return res;
    }

    public class FakeListProvider : IAssemblyListProvider
    {
        public IEnumerable<string> GetAssemblyList()
        {
            var coreFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            var pluginsFolder = Path.Combine(coreFolder, "../plugins/");

            return Directory.EnumerateFiles(pluginsFolder, "*", SearchOption.AllDirectories);
        }
    }

    public override void Start()
    {
        this.AddAssemblyInjector<BaseHost, FakeListProvider>();
        UnityVersionHandler.RecalculateHandlers();
        base.Start();
    }
}
