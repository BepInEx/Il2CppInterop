# Il2CppAssemblyUnhollower

[![GitHub Workflow Status](https://img.shields.io/github/workflow/status/BepInEx/Il2CppAssemblyUnhollower/.NET)](https://github.com/BepInEx/Il2CppAssemblyUnhollower/actions/workflows/dotnet.yml)
[![GitHub release (latest by date)](https://img.shields.io/github/v/release/BepInEx/Il2CppAssemblyUnhollower)](https://github.com/BepInEx/Il2CppAssemblyUnhollower/releases)
[![BepInEx NuGet (Tool)](https://img.shields.io/badge/NuGet-Tool-brightgreen)](https://nuget.bepinex.dev/packages/Il2CppAssemblyUnhollower.Tool)
[![BepInEx NuGet (RuntimeLib)](https://img.shields.io/badge/NuGet-RuntimeLib-brightgreen)](https://nuget.bepinex.dev/packages/Il2CppAssemblyUnhollower.BaseLib)

A tool to generate Managed->IL2CPP proxy assemblies from
 [Il2CppDumper](https://github.com/Perfare/Il2CppDumper)'s or [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)'s output.

This allows the use of IL2CPP domain and objects in it from a managed domain. 
This includes generic types and methods, arrays, and new object creation. Some things may be horribly broken. 
 
This project is a fork of [knah/Il2CppAssemblyUnhollower](https://github.com/knah/Il2CppAssemblyUnhollower) with some additional features and modifications by BepInEx users.
The main changes and features are:

* Optimizations for running Unhollower as a library instead of CLI
* Support for inheriting abstract classes
* Cleaned up `RegisterTypeInIl2Cpp` API
* Fast release schedule: CI builds on GitHub, releases on NuGet

## Usage
  0. Obtain a release using one of the following methods
     * Download latest AssemblyUnhollower release from [GitHub releases](https://github.com/BepInEx/Il2CppAssemblyUnhollower/releases)
     * Reference tool and libraries in your projects via BepInEx NuGet: [Il2CppAssemblyUnhollower.Tool](https://nuget.bepinex.dev/packages/Il2CppAssemblyUnhollower.Tool); [Il2CppAssemblyUnhollower.BaseLib](https://nuget.bepinex.dev/packages/Il2CppAssemblyUnhollower.BaseLib)
     * Clone this repository and build from source
  2. Obtain dummy assemblies using [Il2CppDumper](https://github.com/Perfare/Il2CppDumper) or [Cpp2IL](https://github.com/SamboyCoding/Cpp2IL)
  3. Run `AssemblyUnhollower --input=<path to Il2CppDumper's or Cpp2IL's dummy dll dir> --output=<output directory> --mscorlib=<path to target mscorlib>`    
       
 Resulting assemblies may be used with your favorite loader that offers a Mono domain in the IL2CPP game process, such as [MelonLoader](https://github.com/LavaGang/MelonLoader), [BepInEx](https://github.com/BepInEx/BepInEx) and any other IL2CPP modding tools.
Generated assemblies appear to be invalid according to .NET Core/.NET Framework, but run fine on Mono.

### Command-line parameter reference
```
Usage: AssemblyUnhollower [parameters]
Possible parameters:
        --help, -h, /? - Optional. Show this help
        --verbose - Optional. Produce more console output
        --input=<directory path> - Required. Directory with Il2CppDumper's dummy assemblies
        --output=<directory path> - Required. Directory to put results into
        --mscorlib=<file path> - Required. mscorlib.dll of target runtime system (typically loader's)
        --unity=<directory path> - Optional. Directory with original Unity assemblies for unstripping
        --gameassembly=<file path> - Optional. Path to GameAssembly.dll. Used for certain analyses
        --deobf-uniq-chars=<number> - Optional. How many characters per unique token to use during deobfuscation
        --deobf-uniq-max=<number> - Optional. How many maximum unique tokens per type are allowed during deobfuscation
        --deobf-analyze - Optional. Analyze deobfuscation performance with different parameter values. Will not generate assemblies.
        --blacklist-assembly=<assembly name> - Optional. Don't write specified assembly to output. Can be used multiple times
        --add-prefix-to=<assembly name/namespace> - Optional. Assemblies and namespaces starting with these will get an Il2Cpp prefix in generated assemblies. Can be used multiple times.
        --no-xref-cache - Optional. Don't generate xref scanning cache. All scanning will be done at runtime.
        --no-copy-unhollower-libs - Optional. Don't copy unhollower libraries to output directory
        --obf-regex=<regex> - Optional. Specifies a regex for obfuscated names. All types and members matching will be renamed
        --rename-map=<file path> - Optional. Specifies a file specifying rename map for obfuscated types and members
        --passthrough-names - Optional. If specified, names will be copied from input assemblies as-is without renaming or deobfuscation
Deobfuscation map generation mode:
        --deobf-generate - Generate a deobfuscation map for input files. Will not generate assemblies.
        --deobf-generate-asm=<assembly name> - Optional. Include this assembly for deobfuscation map generation. If none are specified, all assemblies will be included.
        --deobf-generate-new=<directory path> - Required. Specifies the directory with new (obfuscated) assemblies. The --input parameter specifies old (unobfuscated) assemblies. 
```

## Required external setup
Before certain features can be used (namely class injection and delegate conversion), some external setup is required.
 * Set `ClassInjector.Detour` to an implementation of a managed detour with semantics as described in the interface 
 * Alternatively, set `ClassInjector.DoHook` to an Action with same semantics as `DetourAttach` (signature `void**, void*`, first is a pointer to a variable containing pointer to hooked code start, second is a pointer to patch code start, a pointer to call-original code start is written to the first parameter)
 * Call `UnityVersionHandler.Initialize` with appropriate Unity version (default is 2018.4.20)

## Known Issues
 * Non-blittable structs can't be used in delegates
 * Types implementing interfaces, particularly IEnumerable, may be arbitrarily janky with interface methods. Additionally, using them in foreach may result in implicit casts on managed side (instead of `Cast<T>`, see below), leading to exceptions. Use `var` in `foreach` or use `for` instead of `foreach` when possible as a workaround, or cast them to the specific interface you want to use.
 * in/out/ref parameters on generic parameter types (like `out T` in `Dictionary.TryGetValue`) are currently broken
 * Unity unstripping only partially restores types, and certain methods can't be unstripped still; some calls to unstripped methods might result in crashes
 * Unstripped methods with array operations inside contain invalid bytecode
 * Unstripped methods with casts inside will likely throw invalid cast exceptions or produce nulls
 * Some unstripped methods are stubbed with `NotSupportedException` in cases where rewrite failed
 * Nullables have issues when returned from field/property getters and methods

## Generated assemblies caveats
 * IL2CPP types must be cast using `.Cast<T>` or `.TryCast<T>` methods instead of C-style casts or `as`.
 * When IL2CPP code requires a `System.Type`, use `Il2CppType.Of<T>()` instead of `typeof(T)`
 * For IL2CPP delegate types, use the implicit conversion from `System.Action` or `System.Func`, like this: `UnityAction a = new Action(() => {})` or `var x = (UnityAction) new Action(() => {})`
 * IL2CPP assemblies are stripped, so some methods or even classes could be missing compared to pre-IL2CPP assemblies. This is mostly applicable to Unity assemblies.
 * Using generics with value types may lead to exceptions or crashes because of missing method bodies. If a specific value-typed generic signature was not used in original game code, it can't be used externally either.

## Class injection
Starting with version 0.4.0.0, managed classes can be injected into IL2CPP domain. Currently this is fairly limited, but functional enough for GC integration and implementing custom MonoBehaviors.

See [Class injection documentation](Documentation/Class-Injection.md) for more information.
 
## Injected components in asset bundles
 Starting with version 0.4.15.0, injected components can be used in asset bundles. Currently, deserialization for component fields is not supported. Any fields on the component will initially have their default value as defined in the mono assembly.

See [Injected components in asset bundles documentation](Documentation/Injected-Components-In-Asset-Bundles.md) for more information.

## Implementing interfaces with injected types
Starting with 0.4.16.0, injected types can implement IL2CPP interfaces.  

See [Implementing interfaces documentation](Documentation/Implementing-Interfaces.md) for more information.

## PDB generator
UnhollowerPdbGen builds an executable that can be ran to generate a Microsoft PDB file (debug symbols) for GameAssembly.dll based on unhollower-generated names.  
This can be useful for analyzing code of obfuscated games. For unobfuscated games, using [Il2CppInspector](https://github.com/djkaty/Il2CppInspector) would provide way better results for code analysis.  
Generated PDBs were tested with windbg, lldb, WPA viewer/ETL performance analysis and IDA.  
Generated PDBs only include generated methods, and don't include type info, generic method info and IL2CPP internals.   
You need to manually copy the following Microsoft-provided libraries from Visual Studio (or other build tools) for this to work - I'm not redistributing them as license on them is not clear.  
 * `mspdbcore.dll`
 * `msobj140.dll`
 * `tbbmalloc.dll`

These need to be placed next to the built .exe file. Use file search to find `mspdbcore` in VS install. 

## Upcoming features (aka TODO list)
 * Unstripping engine code - fix current issues with unstripping failing or generating invalid bytecode
 * Proper interface support - IL2CPP interfaces will be generated as interfaces and properly implemented by IL2CPP types
 * Improve class injection to support deserializing fields

## Used libraries
Bundled into output files:
 * [iced](https://github.com/0xd4d/iced) by 0xd4d, an x86 disassembler used for xref scanning and possibly more in the future

Used by generator itself:
 * [Mono.Cecil](https://github.com/jbevain/cecil) by jbevain, the main tool to produce assemblies

Parts of source used:
 * [microsoft-pdb](https://github.com/microsoft/microsoft-pdb) for the PDB generator