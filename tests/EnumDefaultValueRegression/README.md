# Enum default-value regression fixture

This fixture checks the five-byte `I4`/`U4` default-value encoding used by
`EnumInjector` for released Unity versions beginning with 2021.2. It compares
the managed writer against an independent C decoder across signed and unsigned
boundary values.

The version boundary comes from archived Unity-generated libil2cpp sources:

- [2021.2.0a21 reads fixed-width `I4`/`U4` values](https://github.com/js6pak/libil2cpp-archive/blob/e2a3a73b0a4e89e354f6261b564e3ad1ace5dfe1/vm/Enum.cpp)
- [2021.2.0b7 uses `ReadCompressedInt32` and `ReadCompressedUInt32`](https://github.com/js6pak/libil2cpp-archive/blob/2eed15c076cdf4d9143404901ec4a1b8f9661295/vm/Enum.cpp)

The runtime reports `System.Version`, which cannot represent Unity prerelease
suffixes. The gate therefore covers released Unity 2021.2 and later; prerelease
builds around the transition are outside the claimed boundary.

Run from the repository root:

```sh
./tests/EnumDefaultValueRegression/run.sh
```

Set `DOTNET` to select a specific `dotnet` host.
