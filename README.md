<p align="center">
    <img src="logo/logo_big.svg" width="300">
</p>

[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/BepInEx/Il2CppInterop/dotnet.yml)](https://github.com/BepInEx/Il2CppInterop/actions/workflows/dotnet.yml)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/BepInEx/Il2CppInterop)](https://github.com/BepInEx/Il2CppInterop/releases)

|                            | CLI                                                                                                                                                                                                                           | Generator                                                                                                                                                                                                                                       | Runtime                                                                                                                                                                                                                                   | HarmonySupport                                                                                                                                                                                                                                                 |
|----------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **NuGet.org** (release)    | [![BepInEx NuGet (CLI)](https://img.shields.io/nuget/v/Il2CppInterop.CLI?label=NuGet)](https://www.nuget.org/packages/Il2CppInterop.CLI)                                                                                      | [![BepInEx NuGet (Generator)](https://img.shields.io/nuget/v/Il2CppInterop.Generator?label=NuGet)](https://www.nuget.org/packages/Il2CppInterop.Generator)                                                                                      | [![BepInEx NuGet (Runtime)](https://img.shields.io/nuget/v/Il2CppInterop.Runtime?label=NuGet)](https://www.nuget.org/packages/Il2CppInterop.Runtime)                                                                                      | [![BepInEx NuGet (HarmonySupport )](https://img.shields.io/nuget/v/Il2CppInterop.HarmonySupport?label=NuGet)](https://www.nuget.org/packages/Il2CppInterop.HarmonySupport )                                                                                    |
| **nuget.bepinex.dev** (CI) | [![BepInEx NuGet (CLI)](https://img.shields.io/endpoint?color=blue&label=NuGet&url=https://shields.kzu.io/vpre/Il2CppInterop.CLI?feed=nuget.bepinex.dev/v3/index.json)](https://nuget.bepinex.dev/packages/Il2CppInterop.CLI) | [![BepInEx NuGet (Generator)](https://img.shields.io/endpoint?color=blue&label=NuGet&url=https://shields.kzu.io/vpre/Il2CppInterop.Generator?feed=nuget.bepinex.dev/v3/index.json)](https://nuget.bepinex.dev/packages/Il2CppInterop.Generator) | [![BepInEx NuGet (Runtime)](https://img.shields.io/endpoint?color=blue&label=NuGet&url=https://shields.kzu.io/vpre/Il2CppInterop.Runtime?feed=nuget.bepinex.dev/v3/index.json)](https://nuget.bepinex.dev/packages/Il2CppInterop.Runtime) | [![BepInEx NuGet (HarmonySupport)](https://img.shields.io/endpoint?color=blue&label=NuGet&url=https://shields.kzu.io/vpre/Il2CppInterop.HarmonySupport?feed=nuget.bepinex.dev/v3/index.json)](https://nuget.bepinex.dev/packages/Il2CppInterop.HarmonySupport) |

***

> **BepInEx fork of Il2CppAssemblyUnhollower is now Il2CppInterop!**
>
> Looking for old README and guides? Check out [`legacy-unhollower` branch](https://github.com/BepInEx/Il2CppInterop/tree/legacy-unhollower).

Il2CppInterop is a framework for bridging together Unity's Il2Cpp and .NET's CoreCLR runtimes. The framework various interoperability tools:

* A tool to generate .NET assemblies from [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)'s output.
* A runtime library to work with Il2Cpp objects in CoreCLR.
* Libraries to integrate other tools with Il2Cpp.

The framework allows the use of Il2Cpp domain and objects in it from a managed .NET domain.
This includes generic types and methods, arrays, and new object creation.

This project started out as fork of [knah/Il2CppAssemblyUnhollower](https://github.com/knah/Il2CppAssemblyUnhollower)
but has been since been modified with new API and fixes to be a standalone project.

## Getting started

For plugin developers:

* [Class injection](https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Class-Injection.md)
* [Implementing interfaces](https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Implementing-Interfaces.md)
* [Components and AssetBundles](https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Injected-Components-In-Asset-Bundles.md)

For tool integrators and advanced user:

* [Using the command line tool](https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Command-Line-Usage.md)
* [Generating assemblies and bootstrapping runtime](https://github.com/BepInEx/Il2CppInterop/blob/master/Documentation/Integration-API.md)


## Used libraries

Bundled into output files:

* [iced](https://github.com/0xd4d/iced) by 0xd4d, an x86 disassembler used for xref scanning and possibly more in the
  future

Used by generator itself:

* [Mono.Cecil](https://github.com/jbevain/cecil) by jbevain, the main tool to produce assemblies
