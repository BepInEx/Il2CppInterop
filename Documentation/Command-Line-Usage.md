# Command-Line Usage

Il2CppInterop provides a CLI tool for common tasks like proxy assembly generation and deobfuscation maps processing.

> **Note:** The CLI tool exists mainly for continuous integration and helper tools. In most cases, you'll want to invoke Il2CppInterop via code!
> Refer to [Il2CppInterop integration guide](Integration-API.md) for information on programmatically invoking Il2CppInterop.

## Getting the tool

The easiest way to obtain the tool is via the `dotnet` tool:

```
dotnet tool install --global --add-source https://nuget.bepinex.dev/v3/index.json Il2CppInterop.CLI
```

Once installed, the tool should be available via `il2cppinterop` command.

Run `il2cppinterop --version` to check if the tool works:


## Basic usage (generate proxy assemblies)

You need to generate proxy assemblies to use Il2CppInterop API and interact with the game via managed code.
These assemblies mimic the original game assemblies in structure, but use Il2CppInterop to forward all calls to the Il2Cpp runtime.
This approach allows interaction with Il2Cpp, almost like a standard UnityMono game.

To generate the proxy assemblies:

1. Download and install [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)
2. Generate dummy assemblies from the game's `GameAssembly.dll`. The easiest approach is to use the following command
  
    ```
    Cpp2IL --game-path <path to game path> --exe-name <name of the game exe> --skip-analysis --skip-metadata-txts --disable-registration-prompts
    ```

    This will generate the assemblies to `cpp2il_out`

3. Use Il2CppInterop CLI to generate the assemblies:

    ```
    il2cppinterop generate --input <path to cpp2il_out folder> --output <directory to which output the proxies> --unity <path to original Unity assemblies> --game-assembly <path to GameAssembly.dll>
    ```

    Refer to `il2cppinterop generate --help` for full description of all arguments

