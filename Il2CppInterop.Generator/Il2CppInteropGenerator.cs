using Il2CppInterop.Common;
using Il2CppInterop.Common.Host;
using Il2CppInterop.Common.XrefScans;
using Il2CppInterop.Generator.Runners;
using Il2CppInterop.Generator.XrefScans;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Generator;

public sealed class Il2CppInteropGenerator : BaseHost
{
    private GeneratorOptions Options { get; init; }

    private readonly List<IRunner> _runners = new();

    private Il2CppInteropGenerator(GeneratorOptions options)
    {
        Options = options;
    }

    public static Il2CppInteropGenerator Create(GeneratorOptions options)
    {
        var generator = new Il2CppInteropGenerator(options);
        generator.AddXrefScanner<Il2CppInteropGenerator, XrefScanImpl>();
        return generator;
    }

    public override void Start()
    {
        base.Start();

        foreach (var runner in _runners)
        {
            Logger.Instance.LogTrace("Running {RunnerName}", runner.GetType().Name);
            runner.Run(Options);
        }
    }

    public override void Dispose()
    {
        foreach (var runner in _runners)
            runner.Dispose();
        _runners.Clear();
        base.Dispose();
    }

    public void Run()
    {
        Start();
        Dispose();
    }

    internal Il2CppInteropGenerator AddRunner<T>() where T : IRunner, new()
    {
        _runners.Add(new T());
        return this;
    }
}
