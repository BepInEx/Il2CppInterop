# PDB generator

This is an executable that can be ran to generate a Microsoft PDB file (debug symbols) for GameAssembly.dll based on unhollower-generated names.

This can be useful for analyzing code of obfuscated games. For unobfuscated games, using [Il2CppInspector](https://github.com/djkaty/Il2CppInspector) might provide better results for code analysis.

Generated PDBs were tested with windbg, lldb, WPA viewer/ETL performance analysis and IDA.

Generated PDBs only include generated methods, and don't include type info, generic method info and IL2CPP internals.

You need to manually copy the following Microsoft-provided libraries from Visual Studio (or other build tools) for this to work. They cannot be redistributed because the license on them is not clear.

 * `mspdbcore.dll`
 * `msobj140.dll`
 * `tbbmalloc.dll`

These need to be placed next to the built executable file. Use file search to find `mspdbcore` in the Visual Studio install directory. By default, they are in `C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\`.
