using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Il2CppInterop.Runtime.Injection
{
    internal class GameManagersAssemblyListFile : IAssemblyListFile
    {
        private List<string> _assemblies = new List<string>();
        private string originalFile;
        private string newFile;

        public bool IsTargetFile(string originalFilePath)
        {
            return originalFilePath.Contains("globalgamemanagers");
        }

        public void Setup(string originalFilePath)
        {
            if (originalFile != null) return;
            originalFile = originalFilePath;
        }

        public void AddAssembly(string name)
        {
            _assemblies.Add(name);
        }

        public string GetOrCreateNewFile()
        {
            if (newFile != null) return newFile;

            newFile = Path.GetTempFileName();
            CreateModifiedFile();
            return newFile;
        }

        private void CreateModifiedFile()
        {
            using var outputStream = File.Open(newFile, FileMode.Create);
            using var outputWriter = new BinaryWriter(outputStream, Encoding.ASCII, false);
            using var inputStream = File.Open(originalFile, FileMode.Open);
            using var inputReader = new BinaryReader(inputStream, Encoding.ASCII, false);

            // Assembly list always starts with UnityEngine.dll
            var startPos = SeekFirstName(inputStream, inputReader);
            if (startPos == -1)
            {
                throw new Exception("Failed to find start of assembly list in globalgamemanagers file!");
            }

            inputStream.Position = 0;
            startPos -= 8;

            for (var i = 0; i < startPos; i++)
            {
                outputWriter.Write(inputReader.ReadByte());
            }

            var assemblyCount = inputReader.ReadInt32();
            List<string> newAssemblyList = new List<string>(assemblyCount + _assemblies.Count);
            List<int> newAssemblyTypes = new List<int>(assemblyCount + _assemblies.Count);
            for (var i = 0; i < assemblyCount; i++)
            {
                newAssemblyList.Add(ReadString(inputReader));
            }

            assemblyCount = inputReader.ReadInt32();
            for (var i = 0; i < assemblyCount; i++)
            {
                newAssemblyTypes.Add(inputReader.ReadInt32());
            }

            newAssemblyList.AddRange(_assemblies);
            newAssemblyTypes.AddRange(_assemblies.Select(_ => 16));

            outputWriter.Write(newAssemblyList.Count);
            foreach (var assemblyName in newAssemblyList)
            {
                WriteString(outputWriter, assemblyName);
            }

            outputWriter.Write(newAssemblyTypes.Count);
            foreach (var assemblyType in newAssemblyTypes)
            {
                outputWriter.Write(assemblyType);
            }

            while (inputStream.Position < inputStream.Length)
            {
                outputWriter.Write(inputReader.ReadByte());
            }
        }

        private static void WriteString(BinaryWriter outputWriter, string @string)
        {
            outputWriter.Write(@string.Length);
            var paddedLenth = (int)(Math.Ceiling(@string.Length / 4f) * 4f);
            for (int i = 0; i < paddedLenth; i++)
            {
                if (i < @string.Length)
                    outputWriter.Write(@string[i]);
                else
                    outputWriter.Write((byte)0);
            }
        }

        private static string ReadString(BinaryReader inputReader)
        {
            var length = inputReader.ReadInt32();
            var paddedLenth = (int)(Math.Ceiling(length / 4f) * 4f);
            StringBuilder sb = new StringBuilder(length);
            for (var j = 0; j < paddedLenth; j++)
            {
                var c = inputReader.ReadChar();
                if (j < length)
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static long SeekFirstName(FileStream inputStream, BinaryReader inputReader)
        {
            while (inputStream.Position < inputStream.Length)
            {
                var currentPos = inputStream.Position;
                var firstChar = inputReader.ReadChar();
                if (firstChar != 'U') continue;

                var nextString = new string(inputReader.ReadChars(14));
                if (!nextString.Equals("nityEngine.dll")) continue;

                return currentPos;
            }

            return -1;
        }
    }
}
