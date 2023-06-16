using System;
using System.IO;
using System.Text.Json.Nodes;

namespace Il2CppInterop.Runtime.Injection
{
    public class AssemblyListFile
    {
        private readonly JsonNode node;
        private readonly JsonArray names;
        private readonly JsonArray types;

        private string newFile;

        public AssemblyListFile(string originalFilePath)
        {
            node = JsonNode.Parse(File.ReadAllText(originalFilePath));
            names = node["names"].AsArray();
            types = node["types"].AsArray();
        }

        public void AddAssembly(string name)
        {
            names.Add(name);
            types.Add(16);
        }

        public string GetTmpFile()
        {
            if (!string.IsNullOrEmpty(newFile)) return newFile;

            var newJson = node.ToJsonString();
            newFile = Path.GetTempFileName();

            File.WriteAllText(newFile, newJson);
            return newFile;
        }
    }
}
