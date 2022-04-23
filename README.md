# Il2CppInterop

[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/BepInEx/Il2CppInterop/.NET)](https://github.com/BepInEx/Il2CppInterop/actions/workflows/dotnet.yml)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/BepInEx/Il2CppInterop)](https://github.com/BepInEx/Il2CppInterop/releases)
[![BepInEx NuGet (Tool)](https://img.shields.io/badge/NuGet-Tool-brightgreen)](https://nuget.bepinex.dev/packages/Il2CppInterop.Tool)
[![BepInEx NuGet (RuntimeLib)](https://img.shields.io/badge/NuGet-RuntimeLib-brightgreen)](https://nuget.bepinex.dev/packages/Il2CppInterop.BaseLib)

A framework for interoperation with Il2Cpp runtime in CoreCLR and mono. The framework includes

* A tool to generate .NET assemblies from [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)'s output
* A runtime library to work with Il2Cpp
* Libraries to integrate other tools with Il2Cpp

The framework allows the use of IL2CPP domain and objects in it from a managed .NET domain. 
This includes generic types and methods, arrays, and new object creation. 
 
This project started out as fork of [knah/Il2CppAssemblyUnhollower](https://github.com/knah/Il2CppAssemblyUnhollower) but has been since forked as a separate project.

## WIP

**NOTE** This refactor is still in progress.

TODOs (in no particular order):

* [x] Rename and retarget all projects
* [x] Remove obsolete projects (BaseLib and PdbGen)
* [ ] Remove Generator's dependency on Runtime library and the BCL
* [ ] Remove all obsoleted functions and classes
* [ ] Clean up CLI (add subcommands for each output type, clean up argument names)
* [ ] Move Harmony Il2Cpp backend to this project
* [ ] Update documentation and READMEs

## Used libraries
Bundled into output files:
 * [iced](https://github.com/0xd4d/iced) by 0xd4d, an x86 disassembler used for xref scanning and possibly more in the future

Used by generator itself:
 * [Mono.Cecil](https://github.com/jbevain/cecil) by jbevain, the main tool to produce assemblies