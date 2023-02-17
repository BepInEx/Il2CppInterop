using System.Text.RegularExpressions;
using CppAst;
using Il2CppInterop.StructGenerator.CodeGen;
using Il2CppInterop.StructGenerator.Resources;
using Il2CppInterop.StructGenerator.Utilities;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.StructGenerator;

public record Il2CppStructWrapperGeneratorOptions(
    string HeadersDirectory,
    string OutputDirectory,
    ILogger? Logger
);

// TODO: Instead expose as source generator (might not be viable since clang is platform-dependent)
public static class Il2CppStructWrapperGenerator
{
    private static readonly Dictionary<int, List<VersionSpecificGenerator>> SGenerators = new();
    internal static ILogger? Logger { get; set; }

    private static int GetMetadataVersion(string libil2CppPath)
    {
        var metadataVersion = -1;
        foreach (var versionContainer in Config.MetadataVersionContainers)
        {
            var fullPath = Path.Combine(libil2CppPath, versionContainer);
            if (File.Exists(fullPath))
            {
                var metadataMatch = Regex.Match(File.ReadAllText(fullPath),
                    @"\(s_GlobalMetadataHeader->version == ([0-9]+)\);");

                if (metadataMatch.Success)
                {
                    metadataVersion = int.Parse(metadataMatch.Groups[1].Value);
                    break;
                }
            }
        }

        return metadataVersion;
    }

    private static VersionSpecificGenerator? VisitClass(CppClass @class, int metadataVersion,
        UnityVersion unityVersion, CppClass[] classes)
    {
        if (Config.ClassForcedIgnores.Contains(@class.Name)) return null;
        if (Config.ClassRenames.TryGetValue(@class.Name, out var rename)) @class.Name = rename;
        if (!Config.ClassToGenerator.TryGetValue(@class.Name, out var generatorType)) return null;
        if (!typeof(VersionSpecificGenerator).IsAssignableFrom(generatorType))
            throw new Exception($"{@class.Name} has an invalid generator");

        var existingVersionGeneratorCount =
            SGenerators[metadataVersion].Count(x => x.GetType() == generatorType);
        var existingGenerators =
            SGenerators.Values.SelectMany(x => x).Where(x => x.GetType() == generatorType).ToList();
        var generator = (VersionSpecificGenerator)Activator.CreateInstance(generatorType,
            $"{metadataVersion}_{existingVersionGeneratorCount}", @class,
            new Func<string, CppClass>(dependencyName => { return classes.Single(x => x.Name == dependencyName); }))!;

        foreach (var field in generator.NativeStructGenerator.FieldsToImport.ToList())
        {
            var cppField = generator.NativeStructGenerator.CppClass.Fields.Single(x => x.Name == field.Name);

            CppClass? typeClass = null;
            if (cppField.Type is CppClass)
                typeClass = (CppClass)cppField.Type;
            if (cppField.Type is CppTypedef typeDef && typeDef.ElementType is CppClass)
                typeClass = (CppClass)typeDef.ElementType;
            if (typeClass != null)
            {
                var gen = VisitClass(typeClass, metadataVersion, unityVersion, classes);
                if (gen == null) continue;
                field.FieldType =
                    $"{gen.HandlerGenerator.HandlerClass.Name}.{gen.NativeStructGenerator.NativeStruct.Name}";
                generator.NativeStructGenerator.FieldsToImport.Remove(field);
                if (Config.ClassToGenerator.ContainsKey(gen.NativeStructGenerator.CppClass.Name))
                    generator.AddExtraUsing(
                        $"Il2CppInterop.Runtime.Runtime.VersionSpecific.{gen.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", string.Empty)}");
            }
        }

        generator.SetupElements();
        foreach (var existingGenerator in existingGenerators)
            if (existingGenerator.NativeStructGenerator.NativeStruct == generator.NativeStructGenerator.NativeStruct)
            {
                existingGenerator.ApplicableVersions.Add(unityVersion);
                return existingGenerator;
            }

        generator.ApplicableVersions.Add(unityVersion);
        SGenerators[metadataVersion].Add(generator);
        return generator;
    }

    public static void Generate(Il2CppStructWrapperGeneratorOptions options)
    {
        Logger = options.Logger;
        if (Directory.Exists(options.OutputDirectory))
            Directory.Delete(options.OutputDirectory, true);
        Directory.CreateDirectory(options.OutputDirectory);
        foreach (var (libil2CppDir, version) in Directory.GetDirectories(options.HeadersDirectory)
                     .Select(x => (x, new UnityVersion(Path.GetFileName(x)))).OrderBy(x => x.Item2))
        {
            var classInternalsPath = Path.Combine(libil2CppDir, "il2cpp-class-internals.h");
            if (!File.Exists(classInternalsPath))
            {
                Logger?.LogWarning(
                    "{} doesn't have il2cpp-class-internals.h - falling back to class-internals.h", version);
                classInternalsPath = Path.Combine(libil2CppDir, "class-internals.h");
                if (!File.Exists(classInternalsPath))
                {
                    Logger?.LogWarning("{} doesn't have class-internals.h", version);
                    continue;
                }
            }

            var objectInternalsPath = Path.Combine(libil2CppDir, "il2cpp-object-internals.h");
            if (!File.Exists(objectInternalsPath))
            {
                Logger?.LogWarning(
                    "{} doesn't have il2cpp-object-internals.h - falling back to object-internals.h", version);
                objectInternalsPath = Path.Combine(libil2CppDir, "object-internals.h");
                if (!File.Exists(objectInternalsPath))
                {
                    Logger?.LogWarning("{} doesn't have object-internals.h", version);
                    continue;
                }
            }

            var metadataVersion = GetMetadataVersion(libil2CppDir);
            if (metadataVersion == -1)
            {
                Logger?.LogWarning("{} has an invalid metadata version", version);
                continue;
            }

            var classInternalsIsTmp = true;
            // Graduated top of my class by the way
            {
                if (!File.Exists($"{classInternalsPath}_backup"))
                {
                    var classInternalsData = File.ReadAllText(classInternalsPath);
                    // I have to do this because the lib I use doesn't recognize these unions, so I have to name them in the most disgusting way imaginable
                    classInternalsData = Regex.Replace(classInternalsData,
                        @"(union.{0,60}?rgctx_data;.*?method(?:Definition|MetadataHandle);.*?});", "$1 runtime_data;",
                        RegexOptions.Singleline);
                    classInternalsData = Regex.Replace(classInternalsData,
                        @"(union.{0,60}?genericMethod;.*?genericContainer(?:Handle)?;.*?});", "$1 generic_data;",
                        RegexOptions.Singleline);

                    File.Move(classInternalsPath, $"{classInternalsPath}_backup");
                    File.WriteAllText(classInternalsPath, classInternalsData);
                }
            }
            if (!SGenerators.ContainsKey(metadataVersion))
                SGenerators[metadataVersion] = new List<VersionSpecificGenerator>();
            var compilation = CppParser.ParseFiles(new List<string> { objectInternalsPath, classInternalsPath },
                new CppParserOptions
                {
                    ParseAsCpp = true,
                    AutoSquashTypedef = false,
                    ParseMacros = true
                });
            Logger?.LogInformation("Parsing {}", version);
            var classes = compilation.Classes.ToArray();
            foreach (var @class in classes) VisitClass(@class, metadataVersion, version, classes);
            if (classInternalsIsTmp)
            {
                File.Delete(classInternalsPath);
                File.Move($"{classInternalsPath}_backup", classInternalsPath);
            }
        }

        Logger?.LogInformation("Building version specific classes");
        // In the eyes of god - I am a disappointment
        Dictionary<Type, Dictionary<UnityVersion, VersionSpecificGenerator>> versionToGeneratorLookup = new();
        foreach (var generator in SGenerators.Values.SelectMany(x => x))
        {
            if (!versionToGeneratorLookup.ContainsKey(generator.GetType()))
                versionToGeneratorLookup[generator.GetType()] =
                    new Dictionary<UnityVersion, VersionSpecificGenerator>();

            foreach (var version in generator.ApplicableVersions)
                versionToGeneratorLookup[generator.GetType()][version] = generator;
        }

        foreach (var kvp in versionToGeneratorLookup)
        {
            VersionSpecificGenerator? last = null;
            foreach (var kvp2 in kvp.Value.Where(kvp2 => last is null || last != kvp2.Value))
            {
                kvp2.Value.HandlerGenerator.HandlerClass.Attributes.Add(
                    $"ApplicableToUnityVersionsSince(\"{kvp2.Key.ToStringShort()}\")");
                last = kvp2.Value;
            }
        }

        foreach (var generator in SGenerators.Values.SelectMany(x => x))
        {
            var generatorOutputDir =
                Path.Combine(options.OutputDirectory,
                    generator.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", string.Empty));
            if (!Directory.Exists(generatorOutputDir))
                Directory.CreateDirectory(generatorOutputDir);
            CodeGenFile file = new()
            {
                Namespace =
                    $"Il2CppInterop.Runtime.Runtime.VersionSpecific.{generator.NativeStructGenerator.CppClass.Name.Replace("Il2Cpp", string.Empty)}",
                Usings =
                {
                    "System",
                    "System.Runtime.InteropServices"
                },
                Elements =
                {
                    generator.HandlerGenerator.HandlerClass
                }
            };
            foreach (var extraUsing in generator.ExtraUsings)
                file.Usings.Add(extraUsing);
            file.WriteTo(Path.Combine(generatorOutputDir,
                $"{generator.NativeStructGenerator.NativeStruct.Name.Replace("Il2Cpp", string.Empty)}.cs"));
        }

        Logger = null;
    }
}
