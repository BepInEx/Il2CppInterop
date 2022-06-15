# Il2CppInterop

[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/BepInEx/Il2CppInterop/.NET)](https://github.com/BepInEx/Il2CppInterop/actions/workflows/dotnet.yml)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/BepInEx/Il2CppInterop)](https://github.com/BepInEx/Il2CppInterop/releases)
[![BepInEx NuGet (CLI)](https://img.shields.io/badge/NuGet-CLI-brightgreen)](https://nuget.bepinex.dev/packages/Il2CppInterop.CLI)
[![BepInEx NuGet (Runtime)](https://img.shields.io/badge/NuGet-Runtime-brightgreen)](https://nuget.bepinex.dev/packages/Il2CppInterop.Runtime)

> **BepInEx fork of Il2CppAssemblyUnhollower is not Il2CppInterop!**
>
> Looking for old README and guides? Check out [`legacy-unhollower` branch](https://github.com/BepInEx/Il2CppInterop/tree/legacy-unhollower).

A framework for interoperation with Il2Cpp runtime in CoreCLR and mono. The framework includes

* A tool to generate .NET assemblies from [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)'s output
* A runtime library to work with Il2Cpp
* Libraries to integrate other tools with Il2Cpp

The framework allows the use of IL2CPP domain and objects in it from a managed .NET domain.
This includes generic types and methods, arrays, and new object creation.

This project started out as fork of [knah/Il2CppAssemblyUnhollower](https://github.com/knah/Il2CppAssemblyUnhollower)
but has been since forked as a separate project.

## Getting started

For plugin developers:

* [Class injection](Documentation/Class-Injection.md)
* [Implementing interfaces](Documentation/Implementing-Interfaces.md)
* [Components and AssetBundles](Documentation/Injected-Components-In-Asset-Bundles.md)

For tool integrators and advanced user:

* [Using the command line tool](Documentation/Command-Line-Usage.md)
* [Generating assemblies and bootstrapping runtime](Documentation/Integration-API.md)


## Used libraries

Bundled into output files:

* [iced](https://github.com/0xd4d/iced) by 0xd4d, an x86 disassembler used for xref scanning and possibly more in the
  future

Used by generator itself:

* [Mono.Cecil](https://github.com/jbevain/cecil) by jbevain, the main tool to produce assemblies
