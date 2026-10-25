using System.Text.RegularExpressions;
using AssetRipper.Primitives;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.StructGenerator;

public static partial class Il2CppStructWrapperGenerator
{
    private static readonly Dictionary<string, Dictionary<int, List<VersionSpecificGenerator>>> SClassNameToGenerators = [];
    internal static ILogger? Logger { get; set; }

    private static VersionSpecificGenerator? VisitClass(CppClass @class, int metadataVersion, UnityVersion unityVersion)
    {
        if (Config.ClassForcedIgnores.Contains(@class.Name))
            return null;
        if (Config.ClassRenames.TryGetValue(@class.Name, out var rename))
            @class.Name = rename;
        if (!Config.ClassNames.Contains(@class.Name))
            return null;

        var generatorsByMetadataVersion = SClassNameToGenerators.GetOrAdd(@class.Name);
        var generatorsForMetadataVersion = generatorsByMetadataVersion.GetOrAdd(metadataVersion);

        var existingVersionGeneratorCount = generatorsForMetadataVersion.Count;
        if (!Config.TryCreateGenerator(@class, $"{metadataVersion}_{existingVersionGeneratorCount}", out var generator))
            return null;

        foreach ((var field, var cppField) in generator.NativeStructGenerator.FieldsToImport)
        {
            var typeClass = cppField.Type.AsClass();

            if (typeClass != null)
            {
                var gen = VisitClass(typeClass, metadataVersion, unityVersion);
                if (gen == null)
                    continue;
                field.FieldType = $"{gen.HandlerGenerator.HandlerClass.Name}.{gen.NativeStructGenerator.NativeStruct.Name}";
                generator.ExtraUsings.Add(gen.Namespace);
            }
        }
        generator.NativeStructGenerator.FieldsToImport.Clear();
        generator.SetupElements();

        var existingGenerators = generatorsByMetadataVersion.SelectMany(x => x.Value);
        foreach (var existingGenerator in existingGenerators)
        {
            if (existingGenerator.NativeStructGenerator.NativeStruct == generator.NativeStructGenerator.NativeStruct)
            {
                existingGenerator.ApplicableVersions.Add(unityVersion);
                return existingGenerator;
            }
        }

        generator.ApplicableVersions.Add(unityVersion);
        generatorsForMetadataVersion.Add(generator);
        return generator;
    }

    public static void Generate(Il2CppStructWrapperGeneratorOptions options)
    {
        Logger = options.Logger;
        if (Directory.Exists(options.OutputDirectory))
            Directory.Delete(options.OutputDirectory, true);
        Directory.CreateDirectory(options.OutputDirectory);
        var previousVersion = UnityVersion.MinVersion;
        foreach (var (headerPath, version) in Directory.GetFiles(options.HeadersDirectory, "*.h")
                     .Select(x => (x, UnityVersion.Parse(Path.GetFileNameWithoutExtension(x)))).OrderBy(x => x.Item2))
        {
            var headerText = File.ReadAllText(headerPath);

            if (!TryGetMetadataVersion(headerText, out var metadataVersion))
            {
                Logger?.LogWarning("{} has an invalid metadata version", version);
                continue;
            }

            var compilation = CppParser.Parse(ProcessHeaderText(headerText),
                new CppParserOptions
                {
                    ParserKind = CppParserKind.Cpp,
                    AutoSquashTypedef = false,
                    ParseMacros = false
                });
            Logger?.LogInformation("Parsing {}", version);
            if (compilation.HasErrors)
            {
                Logger?.LogError("Failed to parse {}", version);
                continue;
            }
            // If this is the first version with this major.minor.build, strip the rest of the information.
            var actualVersion = version.Equals(previousVersion.Major, previousVersion.Minor, previousVersion.Build)
                ? version
                : new UnityVersion(version.Major, version.Minor, version.Build);
            foreach (var @class in compilation.Classes)
            {
                VisitClass(@class, metadataVersion, actualVersion);
            }
            previousVersion = version;
        }

        Logger?.LogInformation("Building version specific classes");

        foreach ((var className, var generatorsByMetadataVersion) in SClassNameToGenerators)
        {
            Dictionary<UnityVersion, VersionSpecificGenerator> versionToGeneratorLookup = [];
            foreach (var generator in generatorsByMetadataVersion.SelectMany(x => x.Value))
            {
                foreach (var version in generator.ApplicableVersions)
                {
                    versionToGeneratorLookup.Add(version, generator);
                }
            }

            VersionSpecificGenerator? last = null;
            List<(UnityVersion Version, VersionSpecificGenerator Generator)> list = [];
            foreach ((var version, var generator) in versionToGeneratorLookup.OrderBy(kvp => kvp.Key))
            {
                if (last is not null && last == generator)
                    continue;

                var versionString = version is { Type: UnityVersionType.Alpha, TypeNumber: 0 }
                    ? version.ToStringWithoutType()
                    : version.ToString();
                generator.HandlerGenerator.HandlerClass.Attributes.Add($"ApplicableToUnityVersionsSince(\"{versionString}\")");
                last = generator;
                list.Add((version, generator));
            }

            var generatorOutputDirectory = Path.Join(options.OutputDirectory, className.Replace("Il2Cpp", null));
            foreach (var generator in generatorsByMetadataVersion.SelectMany(x => x.Value))
            {
                Directory.CreateDirectory(generatorOutputDirectory);
                CodeGenFile file = new()
                {
                    Namespace = generator.Namespace,
                    Usings =
                    {
                        "System.Runtime.InteropServices"
                    },
                    Elements =
                    {
                        generator.HandlerGenerator.HandlerClass
                    }
                };
                file.Usings.AddRange(generator.ExtraUsings);
                file.WriteTo(Path.Join(generatorOutputDirectory, $"{generator.GeneratorName}_{generator.MetadataSuffix}.cs"));
            }

            // Use the first generator for this class to generate the interface file, since all generators for a given class will have the same interface.
            var firstGenerator = list[0].Generator;
            {
                var interfacesFile = firstGenerator.GenerateInterfacesFile();
                interfacesFile.WriteTo(Path.Join(generatorOutputDirectory, "Interfaces.cs"));
            }

            // Generate the UnityVersionHandler partial class for this class
            {
                var unityVersionHandlerClass = new CodeGenClass(ElementProtection.Public, "UnityVersionHandler")
                {
                    IsPartial = true,
                    IsStatic = true,
                    Properties =
                    {
                        new CodeGenProperty(firstGenerator.HandlerInterface, ElementProtection.Private, $"{firstGenerator.GeneratorName}StructHandler")
                        {
                            EmptyGet = true,
                            EmptySet = true,
                            IsStatic = true,
                            Initializer = $"{firstGenerator.HandlerName}.Instance"
                        }
                    },
                    Methods =
                    {
                        new CodeGenMethod("void", ElementProtection.Private, $"Set{firstGenerator.GeneratorName}StructHandler")
                        {
                            IsStatic = true,
                            Parameters =
                            {
                                new CodeGenParameter("UnityVersion", "version")
                            },
                            MethodBodyBuilder = writer =>
                            {
                                if (list.Count == 1)
                                {
                                    writer.WriteLine($"{firstGenerator.GeneratorName}StructHandler = {firstGenerator.HandlerName}.Instance;");
                                    return;
                                }

                                for (var i = list.Count - 1; i >= 0; i--)
                                {
                                    var (version, generator) = list[i];
                                    if (i == 0)
                                    {
                                        writer.WriteLine("else");
                                    }
                                    else
                                    {
                                        writer.Write(i == list.Count - 1 ? "if" : "else if");
                                        writer.WriteLine($" (version >= new UnityVersion({version.Major}, {version.Minor}, {version.Build}, UnityVersionType.{version.Type}, {version.TypeNumber}))");
                                    }
                                    writer.WriteLine("{");
                                    writer.Indent++;
                                    writer.WriteLine($"{generator.GeneratorName}StructHandler = {generator.HandlerName}.Instance;");
                                    writer.Indent--;
                                    writer.WriteLine("}");
                                }
                            }
                        }
                    }
                };
                var unityVersionHandlerFile = new CodeGenFile()
                {
                    Namespace = "Il2CppInterop.Runtime.Structs",
                    Usings =
                    {
                        "AssetRipper.Primitives",
                        firstGenerator.Namespace,
                    },
                    Elements =
                    {
                        unityVersionHandlerClass
                    }
                };
                unityVersionHandlerFile.WriteTo(Path.Join(generatorOutputDirectory, "UnityVersionHandler.cs"));
            }
        }

        Logger = null;
    }

    private static string ProcessHeaderText(string headerText)
    {
        const string HeaderPrefix = """
            #line 1 "Prefix.h"
            typedef int int32_t;
            typedef unsigned int uint32_t;
            typedef short int16_t;
            typedef unsigned short uint16_t;
            typedef char int8_t;
            typedef unsigned char uint8_t;
            typedef long long int64_t;
            typedef unsigned long long uint64_t;
            typedef long intptr_t;
            typedef unsigned long uintptr_t;
            """;

        string processedHeaderText;
        if (headerText.Contains("struct ParameterInfo", StringComparison.Ordinal))
        {
            processedHeaderText = $"""
                {HeaderPrefix}
                #line 1 "Header.h"
                {headerText}
                """;
        }
        else
        {
            // ParameterInfo was removed in v27, but we add it back in manually.
            processedHeaderText = $$"""
                {{HeaderPrefix}}
                #line 1 "ParameterInfo.h"
                typedef struct Il2CppType Il2CppType;
                typedef struct ParameterInfo
                {
                    const Il2CppType* parameter_type;
                } ParameterInfo;
                #line 1 "Header.h"
                {{headerText.Replace("const Il2CppType** parameters;", "const ParameterInfo* parameters;")}}
                """;
        }

        return processedHeaderText;
    }

    private static bool TryGetMetadataVersion(string headerText, out int metadataVersion)
    {
        var match = MetadataVersionRegex.Match(headerText);
        if (match.Success)
        {
            return int.TryParse(match.Groups[1].Value, out metadataVersion);
        }
        else
        {
            metadataVersion = default;
            return false;
        }
    }

    [GeneratedRegex(@"const int METADATA_VERSION = ([0-9]+);")]
    private static partial Regex MetadataVersionRegex { get; }
}
