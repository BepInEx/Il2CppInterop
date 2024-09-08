using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using AsmResolver.DotNet;

namespace Il2CppInterop.Generator;

public class GeneratorOptions
{
    public List<AssemblyDefinition>? Source { get; set; }
    public string? OutputDir { get; set; }

    public string? UnityBaseLibsDir { get; set; }
    public List<string> AdditionalAssembliesBlacklist { get; } = new();
    public int TypeDeobfuscationCharsPerUniquifier { get; set; } = 2;
    public int TypeDeobfuscationMaxUniquifiers { get; set; } = 10;
    public string? GameAssemblyPath { get; set; }
    public bool Verbose { get; set; }
    public bool NoXrefCache { get; set; }
    public Regex? ObfuscatedNamesRegex { get; set; }
    public Dictionary<string, string> RenameMap { get; } = new();
    public bool PassthroughNames { get; set; }
    public bool Parallel { get; set; } = true;

    public PrefixMode Il2CppPrefixMode { get; set; } = PrefixMode.OptIn;
    public HashSet<string> NamespacesAndAssembliesToPrefix { get; } =
        new() { "System", "mscorlib", "Microsoft", "Mono", "I18N" };
    public HashSet<string> NamespacesAndAssembliesToNotPrefix { get; } =
        new() { "Assembly-CSharp", "Unity" };

    public List<string> DeobfuscationGenerationAssemblies { get; } = new();
    public string? DeobfuscationNewAssembliesPath { get; set; }

    /// <summary>
    ///     Reads a rename map from the specified name into the specified instance of options
    /// </summary>
    public void ReadRenameMap(string fileName)
    {
        using var fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read);
        ReadRenameMap(fileStream, fileName.EndsWith(".gz"));
    }

    /// <summary>
    ///     Reads a rename map from the specified name into the specified instance of options.
    ///     The stream is not closed by this method.
    /// </summary>
    public void ReadRenameMap(Stream fileStream, bool isGzip)
    {
        if (isGzip)
        {
            using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress, true);
            ReadRenameMap(gzipStream, false);
            return;
        }

        using var reader = new StreamReader(fileStream, Encoding.UTF8, false, 65536, true);
        while (!reader.EndOfStream)
        {
            var line = reader.ReadLine();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;
            var split = line.Split(';');
            if (split.Length < 2) continue;
            RenameMap[split[0]] = split[1];
        }
    }

    public enum PrefixMode
    {
        /// <summary>
        ///     Only specified namespaces and assemblies will be renamed.
        /// </summary>
        OptIn,
        /// <summary>
        ///     Only specified namespaces and assemblies will not be renamed.
        /// </summary>
        OptOut
    }
}
