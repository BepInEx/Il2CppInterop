using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Il2CppInterop.Runtime.Injection
{
    public class AssemblyListFile : IDisposable
    {
        private readonly string assemblyNamesFile;
        private readonly JsonNode node;
        private readonly JsonArray names;
        private readonly JsonArray types;

        public AssemblyListFile()
        {
            var executablePath = Environment.GetEnvironmentVariable("DOORSTOP_PROCESS_PATH");
            var processPath = Path.GetFileNameWithoutExtension(executablePath);
            assemblyNamesFile = $"{processPath}_Data/ScriptingAssemblies.json";
            var assemblyNamesFileBackup = $"{processPath}_Data/ScriptingAssemblies_BACKUP.json";
            if (!File.Exists(assemblyNamesFileBackup))
            {
                File.Copy(assemblyNamesFile, assemblyNamesFileBackup);
            }

            node = JsonNode.Parse(File.ReadAllText(assemblyNamesFileBackup));
            names = node["names"].AsArray();
            types = node["types"].AsArray();
        }

        public void AddAssembly(string name)
        {
            names.Add(name);
            types.Add(16);
        }

        public void Dispose()
        {
            var newJson = node.ToJsonString();
            File.WriteAllText(assemblyNamesFile, newJson);
        }
    }
}
