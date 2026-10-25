# Integrating Il2CppInterop into plugin loaders

> **Note:** This guide is intended for plugin loader developers and maintainers. *Plugin developers* may refer to other guides.


## Generating proxy assemblies in loaders

Assembly generation can be done either via [the CLI](Command-Line-Usage.md) or directly via [Il2CppInterop.Generator](https://nuget.bepinex.dev/packages/Il2CppInterop.Generator) package.

The generator is a list of Cpp2IL processing layers. `Il2CppGame.Process` loads the game's binary and metadata with Cpp2IL, runs the layers, and writes the assemblies with an output format:

```cs
Il2CppGame.Process(
    gameExePath, // Path to the game's executable, next to GameAssembly.dll
    outputDirectory, // Path to which generate the assemblies
    new AsmResolverDllOutputFormatBinding(),
    Il2CppGame.GetDefaultProcessingLayers(),
    [new(UnstripBaseProcessingLayer.DirectoryKey, unityLibrariesDirectory)]);
```

Unstripping restores members that the game's build removed, from Unity's managed libraries (`UnityEngine.dll` etc.).
`UnstripBaseProcessingLayer.DirectoryKey` names a directory that contains them. To pass assemblies that are already loaded instead,
a processing layer can put an `IReadOnlyList<AsmResolver.DotNet.AssemblyDefinition>` into the application context under
`UnstripBaseProcessingLayer.AssembliesKey` in its `PreProcess` method. Without either, the `UnstripProcessingLayer` only
logs a warning, so remove it from the list.

The unstrip directory must include all files recursively contained in both of these Unity directories (shown for Windows):

- `Editor\Data\MonoBleedingEdge\lib\mono\unityaot-win32`
- `Editor\Data\PlaybackEngines\windowsstandalonesupport\Variations\win64_player_nondevelopment_il2cpp\Data\Managed`

These paths are relative to the Unity installation. The corresponding paths differ on other platforms.

The generator logs through Cpp2IL's logger (`Cpp2IL.Core.Logging.Logger.InfoLog`, `WarningLog`, `ErrorLog` and `VerboseLog`).

## Initializing the runtime

Once all assemblies are generated and loaded, you can initialize the Il2CppInterop runtime.
The runtime is available via [Il2CppInterop.Runtime](https://nuget.bepinex.dev/packages/Il2CppInterop.Runtime) package.
Start it after Il2Cpp is initialized (`il2cpp_init` has returned):

```cs
Il2CppInteropRuntime.Create(new RuntimeConfiguration
    {
        UnityVersion = unityVersion, // The game's Unity version
        DetourProvider = detourProvider, // Your IDetourProvider
    })
    .AddLogger(logger) // Optional: a Microsoft.Extensions.Logging.ILogger
    .Start();
```

The runtime installs native detours on a few Il2Cpp functions for class injection. `IDetourProvider.Create` creates one
and returns the trampoline that calls the original function. The runtime does not keep the returned `IDisposable`,
so the provider has to keep each detour alive for as long as the game runs.

## Patching interop methods with Harmony

[Il2CppInterop.HarmonySupport](https://nuget.bepinex.dev/packages/Il2CppInterop.HarmonySupport) lets HarmonyX patch the methods of interop types.
`Il2CppInteropDetourFactory` creates a native detour on the Il2Cpp method for those, and hands all other methods to the fallback factory.
HarmonyX gets its detour factory from MonoMod's global detour context, so install it there:

```cs
DetourContext.SetGlobalContext(new DetourFactoryContext(new Il2CppInteropDetourFactory()));
```

Do not make it `DetourFactory.Current` with `DetourFactory.SetCurrentFactory`. `Il2CppInteropDetour` creates its own
detours through `DetourFactory.Current`, so it would call itself.
