using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Il2CppInterop.Runtime.Injection
{
    internal class JSONAssemblyListFile : IAssemblyListFile
    {
        private JsonNode node;
        private JsonArray names;
        private JsonArray types;

        private string newFile;

        public void Setup(string originalFilePath)
        {
            if (node != null) return;

            node = JsonNode.Parse(File.ReadAllText(originalFilePath));
            names = node["names"].AsArray();
            types = node["types"].AsArray();
        }

        public bool IsTargetFile(string originalFilePath)
        {
            return originalFilePath.Contains("ScriptingAssemblies.json");
        }

        public void AddAssembly(string name)
        {
            names.Add(name);
            types.Add(16);
        }

        public string GetOrCreateNewFile()
        {
            if (!string.IsNullOrEmpty(newFile)) return newFile;

            var newJson = node.ToJsonString();
            newFile = Path.GetTempFileName();

            File.WriteAllText(newFile, newJson);
            return newFile;
        }
    }
}
