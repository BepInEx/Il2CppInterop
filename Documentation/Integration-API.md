# Integrating Il2CppInterop into plugin loaders

> **Note:** This guide is intended for plugin loader developers and maintainers. *Plugin developers* may refer to other guides.


## Generating proxy assemblies in loaders

> TODO: Expand

Assembly generation can be done either via [the CLI](Command-Line-Usage.md) or directly via [Il2CppInterop.Generator](https://nuget.bepinex.dev/packages/Il2CppInterop.Generator) package.

Example of Il2CppInterop.Generator usage:

```cs
var opts = new GeneratorOptions
{
    GameAssemblyPath = GameAssemblyPath, // Path to GameAssembly.dll
    Source = sourceAssemblies, // List of Cpp2Il dummy assemblies loaded into Cecil
    OutputDir = IL2CPPInteropAssemblyPath, // Path to which generate the assemblies
    UnityBaseLibsDir = Directory.Exists(UnityBaseLibsDirectory) ? UnityBaseLibsDirectory : null // Path to managed Unity core libraries (UnityEngine.dll etc)
};

Il2CppInteropGenerator.Create(opts)
                      .AddInteropAssemblyGenerator()
                      .Run();

// Dispose of sourceAssemblies, etc cleanup
```

Logging is supported via `.AddLogger(ILogger)` helper function.

## Initializing the runtime

Once all assemblies are generated and loaded, you can initialize the Il2CppInterop runtime.
The runtime is available via [Il2CppInterop.Runtime](https://nuget.bepinex.dev/packages/Il2CppInterop.Runtime) package.

Example: