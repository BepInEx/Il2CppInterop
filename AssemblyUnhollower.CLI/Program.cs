using System.CommandLine;
using System.CommandLine.NamingConventionBinder;
using System.Text.RegularExpressions;
using AssemblyUnhollower;
using Microsoft.Extensions.Logging;
using Mono.Cecil;
using UnhollowerBaseLib;

var command = new RootCommand
{
    new Option<bool>("--verbose", "Produce more verbose output"),
    new Option<DirectoryInfo>("--input", "Directory with Il2CppDumper's dummy assemblies") {IsRequired = true}
        .ExistingOnly(),
    new Option<DirectoryInfo>("--output", "Directory to write generated assemblies to") {IsRequired = true},
    new Option<DirectoryInfo>("--system-libs",
            "Directory with system libraries of target runtime system (typically loader's)")
        {IsRequired = true}.ExistingOnly(),
    new Option<FileInfo>("--mscorlib", "Deprecated. mscorlib.dll of target runtime system (typically loader's)")
        .ExistingOnly(),
    new Option<DirectoryInfo>("--unity", "Directory with original Unity assemblies for unstripping").ExistingOnly(),
    new Option<FileInfo>("--gameassembly", "Path to GameAssembly.dll. Used for certain analyses").ExistingOnly(),
    new Option<int>("--deobf-uniq-chars", "How many characters per unique token to use during deobfuscation"),
    new Option<int>("--deobf-uniq-max", "How many maximum unique tokens per type are allowed during deobfuscation"),
    new Option<bool>("--deobf-analyze",
        "Analyze deobfuscation performance with different parameter values. Will not generate assemblies."),
    new Option<string[]>("--blacklist-assembly", "Don't write specified assembly to output. Allows multiple values."),
    new Option<string[]>("--add-prefix-to",
        "Assemblies and namespaces starting with these will get an Il2Cpp prefix in generated assemblies. Allows multiple values."),
    new Option<bool>("--no-xref-cache", "Don't generate xref scanning cache. All scanning will be done at runtime."),
    new Option<bool>("--no-copy-unhollower-libs", "Don't copy unhollower libraries to output directory."),
    new Option<Regex>("--obf-regex",
        "Specifies a regex for obfuscated names. All types and members matching will be renamed."),
    new Option<FileInfo>("--rename-map", "Specifies a file specifying rename map for obfuscated types and members.")
        .ExistingOnly(),
    new Option<bool>("--passthrough-names",
        "If specified, names will be copied from input assemblies as-is without renaming or deobfuscation."),
    new Option<bool>("--deobf-generate", "Generate a deobfuscation map for input files. Will not generate assemblies."),
    new Option<string[]>("--deobf-generate-asm",
        "Include these assemblies for deobfuscation map generation. If none are specified, all assemblies will be included."),
    new Option<DirectoryInfo>("--deobf-generate-new",
        "Specifies the directory with new (obfuscated) assemblies. The --input parameter specifies old (unobfuscated) assemblies.")
};

command.Description = "Generate Managed<->IL2CPP proxy assemblies from Il2CppDumper's or Cpp2IL's output.";
command.Handler = CommandHandler.Create((CmdOptions opts) =>
{
    var loggerFactory = LoggerFactory.Create(builder =>
    {
        builder
            .AddFilter("Unhollower", opts.Verbose ? LogLevel.Trace : LogLevel.Warning)
            .AddSimpleConsole(opt => { opt.SingleLine = true; });
    });

    var logger = loggerFactory.CreateLogger("Unhollower");
    LogSupport.ErrorHandler += s => logger.LogError("{Msg}", s);
    LogSupport.WarningHandler += s => logger.LogWarning("{Msg}", s);
    LogSupport.InfoHandler += s => logger.LogInformation("{Msg}", s);
    LogSupport.TraceHandler += s => logger.LogTrace("{Msg}", s);

    // TODO: Instead, separate CLI into three verbs: deobf-analyze, deobf-generate and "none" (normal unhollowing)
    if (opts.DeobfAnalyze && opts.DeobfGenerate)
        throw new InvalidOperationException(
            "Deobfuscation map generation and analysis is not supported at the same time!");

    var unhollowerOptions = opts.BuildOptions();

    if (opts.DeobfAnalyze)
        DeobfuscationMapGenerator.AnalyzeDeobfuscationParams(unhollowerOptions);
    else if (opts.DeobfGenerate)
        DeobfuscationMapGenerator.GenerateDeobfuscationMap(unhollowerOptions);
    else
        UnhollowedAssemblyGenerator.GenerateUnhollowedAssemblies(unhollowerOptions);
});
return command.Invoke(args);

internal record CmdOptions(
    bool Verbose,
    DirectoryInfo Input,
    DirectoryInfo Output,
    DirectoryInfo SystemLibs,
    FileInfo? Mscorlib,
    DirectoryInfo? Unity,
    FileInfo? GameAssembly,
    int DeobfUniqChars,
    int DeobfUniqMax,
    bool DeobfAnalyze,
    string[]? BlacklistAssembly,
    string[]? AddPrefixTo,
    bool NoXrefCache,
    bool NoCopyUnhollowerLibs,
    Regex? ObfRegex,
    FileInfo? RenameMap,
    bool PassthroughNames,
    bool DeobfGenerate,
    string[]? DeobfGenerateAsm,
    DirectoryInfo? DeobfGenerateNew
)
{
    public UnhollowerOptions BuildOptions()
    {
        var result = new UnhollowerOptions
        {
            Verbose = Verbose,
            NoXrefCache = NoXrefCache,
            NoCopyUnhollowerLibs = NoCopyUnhollowerLibs,
            Source = Input.EnumerateFiles("*.dll").Select(f => AssemblyDefinition.ReadAssembly(f.FullName)).ToList(),
            OutputDir = Output.FullName,
            SystemLibrariesPath = SystemLibs.FullName,
            MscorlibPath = Mscorlib?.FullName ?? "",
            UnityBaseLibsDir = Unity?.FullName,
            GameAssemblyPath = GameAssembly?.FullName ?? "",
            TypeDeobfuscationCharsPerUniquifier = DeobfUniqChars,
            TypeDeobfuscationMaxUniquifiers = DeobfUniqMax,
            ObfuscatedNamesRegex = ObfRegex,
            DeobfuscationNewAssembliesPath = DeobfGenerateNew?.FullName ?? "",
            PassthroughNames = PassthroughNames
        };
        result.AdditionalAssembliesBlacklist.AddRange(BlacklistAssembly ?? Array.Empty<string>());
        if (AddPrefixTo is not null)
            foreach (var s in AddPrefixTo)
                result.NamespacesAndAssembliesToPrefix.Add(s);
        if (RenameMap is not null)
            result.ReadRenameMap(RenameMap.FullName);
        result.DeobfuscationGenerationAssemblies.AddRange(DeobfGenerateAsm ?? Array.Empty<string>());
        return result;
    }
}