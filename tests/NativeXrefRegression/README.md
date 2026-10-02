# Native xref architecture regressions

Run commands from a directory **outside this checkout** when using .NET 9;
the repository's `global.json` selects .NET 8. No game files are needed.

```sh
checkout=/absolute/path/to/Il2CppInterop
dotnet9=/absolute/path/to/dotnet9/dotnet
output=/absolute/path/to/temporary/xref-output
cd /tmp
"$dotnet9" build "$checkout/tests/NativeXrefRegression/NativeXrefRegression.csproj" \
  -p:GeneratePackageOnBuild=false -p:NuGetAudit=false -o "$output"
"$dotnet9" "$output/NativeXrefRegression.dll"
```

On ARM64 this runs five checks: both low-level entry points reject native
scanning before pointer access; an uncached proxy method rejects scanning
before metadata initialization; empty cached results and ordinary managed
methods retain their existing behavior. On x86/x64 it runs four checks,
including decoding a synthetic CALL/RET byte buffer without executing it.

On Apple Silicon with Rosetta and the .NET 9 x64 runtime installed, run the
same assembly in a real x64 process:

```sh
arch -x86_64 /absolute/path/to/x64-dotnet/dotnet "$output/NativeXrefRegression.dll"
```

The Class::Init regression compiles the exact `Class::Init` region extracted
from `InjectorHelpers.cs` in a temporary project. Signature lookup, export
lookup and logging use test doubles; the resolver and fallback logic come
from production source. It checks that ARM skips every x86 signature, x64
retains signature selection, the existing export fallback order is preserved,
and missing substitutes throw. No returned native delegate is invoked.

```sh
python3 "$checkout/tests/NativeXrefRegression/class-init-regression.py" \
  --dotnet "$dotnet9" --x64-dotnet /absolute/path/to/x64-dotnet/dotnet
```

Omit `--x64-dotnet` to run only the current architecture. This fixture avoids
the game's module-dependent `InjectorHelpers` type initializer; it verifies
resolver control flow, not real IL2CPP class initialization or hook safety.
The xref guard does not add ARM decoding. Generator decoding of explicitly
selected input images is unchanged.
