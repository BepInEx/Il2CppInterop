namespace Il2CppInterop.Generator.Runners;

internal interface IRunner : IDisposable
{
    void Run(GeneratorOptions options);
}
