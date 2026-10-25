using Microsoft.Extensions.Logging;

namespace Il2CppInterop.StructGenerator;

public record Il2CppStructWrapperGeneratorOptions(
    string HeadersDirectory,
    string OutputDirectory,
    ILogger? Logger
);
