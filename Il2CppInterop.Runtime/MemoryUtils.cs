using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

public class MemoryUtils
{
    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool VirtualProtect(IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern int VirtualQuery(IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

    public static nint FindSignatureInModule(ProcessModule module, SignatureDefinition sigDef)
    {
        GetModuleRegions(module, out var protectedRegions);
        SetModuleRegions(protectedRegions, PAGE_EXECUTE_READWRITE);
        var ptr = FindSignatureInBlock(
            module.BaseAddress,
            module.ModuleMemorySize,
            sigDef.pattern,
            sigDef.mask,
            sigDef.offset
        );
        SetModuleRegions(protectedRegions);
        if (ptr != 0 && sigDef.xref)
            ptr = XrefScannerLowLevel.JumpTargets(ptr).FirstOrDefault();
        return ptr;
    }

    public static nint FindSignatureInBlock(nint block, long blockSize, string pattern, string mask, long sigOffset = 0)
    {
        return FindSignatureInBlock(block, blockSize, pattern.ToCharArray(), mask.ToCharArray(), sigOffset);
    }

    public static unsafe nint FindSignatureInBlock(nint block, long blockSize, char[] pattern, char[] mask,
        long sigOffset = 0)
    {
        for (long address = 0; address < blockSize; address++)
        {
            var found = true;
            for (uint offset = 0; offset < mask.Length; offset++)
                if (*(byte*)(address + block + offset) != (byte)pattern[offset] && mask[offset] != '?')
                {
                    found = false;
                    break;
                }

            if (found)
                return (nint)(address + block + sigOffset);
        }

        return 0;
    }

    public static void GetModuleRegions(ProcessModule module, out List<MEMORY_BASIC_INFORMATION> protectedRegions)
    {
        protectedRegions = [];
        IntPtr moduleEndAddress = (IntPtr)((long)module.BaseAddress + module.ModuleMemorySize);
        var currentAddress = module.BaseAddress;
        while (currentAddress.ToInt64() < moduleEndAddress.ToInt64())
        {
            var result = VirtualQuery(currentAddress, out var memoryInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));
            if (result == 0)
                // Error occurred or reached the end of the module's memory space
                break;
            else
                protectedRegions.Add(memoryInfo);

            // Move to the next memory region
            currentAddress = (IntPtr)((long)memoryInfo.BaseAddress + (long)memoryInfo.RegionSize);
        }
    }

    public static void SetModuleRegions(List<MEMORY_BASIC_INFORMATION> protectedRegions, uint? newProtection = null)
    {
        foreach (var region in protectedRegions)
        {
            var result = VirtualProtect(region.BaseAddress, (uint)region.RegionSize, newProtection ?? region.Protect, out _);
            if (!result)
            {
                var error = Marshal.GetLastWin32Error();
                Logger.Instance.LogError("VirtualProtect failed with error code {error}", error);
            }
        }
    }

    public static void RuntimeModuleDump(ILogger logger, out byte[] il2cppBytes, out byte[] metadataBytes, byte[] metadataSignatureToScan, byte[] magicToFix, int metadataSignatureOffset = 252)
    {
        Process process = Process.GetCurrentProcess();
        var module = process
            .Modules.OfType<ProcessModule>()
            .Single((x) => x.ModuleName is "GameAssembly.dll" or "GameAssembly.so" or "UserAssembly.dll"); ;
        if (module.ModuleName == null)
        {
            logger.LogError("GameAssembly.dll or GameAssembly.so or UserAssembly.dll not found");
            il2cppBytes = [];
            metadataBytes = [];
            return;
        }
        var moduleBytes = new byte[module.ModuleMemorySize];
        GetModuleRegions(module, out var protectedRegions);
        SetModuleRegions(protectedRegions, PAGE_EXECUTE_READWRITE);
        if (!ReadProcessMemory(process.Handle, module.BaseAddress, moduleBytes, module.ModuleMemorySize, out _))
        {
            logger.LogError("Failed to read process memory");
            il2cppBytes = [];
            metadataBytes = [];
            return;
        }
        SetModuleRegions(protectedRegions);
        using (var stream = new MemoryStream(moduleBytes))
        using (var reader = new BinaryReader(stream))
        using (var writer = new BinaryWriter(stream))
        {
            // Parse the PE header to get the section headers
            stream.Position = 0x3C;
            var peHeaderOffset = reader.ReadInt32();
            logger.LogDebug("peHeaderOffset: {peHeaderOffset}", peHeaderOffset);
            stream.Position = peHeaderOffset + 6;
            var numberOfSections = reader.ReadUInt16();
            var timeDateStame = reader.ReadUInt32();
            var pointerToSymbolTable = reader.ReadUInt32();
            var numberOfSymbols = reader.ReadUInt32();
            var sizeOfOptionalHeader = reader.ReadUInt16();
            var characteristics = reader.ReadUInt16();
            var section0StartPosition = (int)stream.Position + sizeOfOptionalHeader;

            // Update each section header's PointerToRawData and SizeOfRawData fields
            for (var i = 0; i < numberOfSections; i++)
            {
                logger.LogDebug("numberOfSections: {numberOfSections}", numberOfSections);
                stream.Position = section0StartPosition + (i * 40);
                logger.LogDebug("stream.Position: {stream.Position}", stream.Position);
                var sectionNameBytes = reader.ReadBytes(8);
                var sectionName = Encoding.ASCII.GetString(sectionNameBytes).TrimEnd('\0');
                logger.LogDebug("sectionName: {sectionName}", sectionName);
                var virtualSize = reader.ReadUInt32();
                logger.LogDebug("VirtualSize: {virtualSize:X} stream.Position: {stream.Position}", virtualSize, stream.Position);
                var virtualAddress = reader.ReadUInt32();
                logger.LogDebug("VirtualAddress: {virtualAddress:X} stream.Position: {stream.Position}", virtualAddress, stream.Position);
                writer.Write(virtualSize);
                logger.LogDebug("Replacing SizeOfRawData with VirtualSize value of {virtualSize:X} stream.Position: {stream.Position}", virtualSize, stream.Position);
                writer.Write(virtualAddress);
                logger.LogDebug("Replacing SizeOfRawData with VirtualSize value of {virtualAddress:X} stream.Position: {stream.Position}", virtualAddress, stream.Position);
            }
        }

        logger.LogDebug("Processed {module.ModuleName}", module.ModuleName);
        il2cppBytes = moduleBytes;

        var byteArray = moduleBytes;

        // search for pattern in the byte array
        var index = Array.IndexOf(byteArray, (byte)metadataSignatureToScan[0]);
        while (index >= 0 && index <= byteArray.Length - metadataSignatureToScan.Length)
        {
            if (byteArray.Skip(index).Take(metadataSignatureToScan.Length).SequenceEqual(metadataSignatureToScan))
            {
                // pattern found, trim everything before it
                var trimmedArray = new byte[byteArray.Length - index + metadataSignatureOffset];
                // copy the metadata bytes
                Array.Copy(byteArray, index - metadataSignatureOffset, trimmedArray, 0, trimmedArray.Length);
                // this is required for il2cppdumper to work
                if (magicToFix.Length >= 0)
                    Array.Copy(magicToFix, 0, trimmedArray, 0, magicToFix.Length);

                byteArray = trimmedArray;
                break;
            }
            index = Array.IndexOf(byteArray, (byte)metadataSignatureToScan[0], index + 1);
        }

        logger.LogDebug("Processed global-metadata.dat");
        metadataBytes = byteArray;
        return;
    }

    public static void ValidateMetadata(ILogger logger, string metadataPath, byte[] il2cppBytes, ref byte[] metadataBytes)
    {
        //metadataBytes will equal il2cppBytes if the search pattern did not match.
        //In this case, global-metadata.dat is not embedded in GameAssembly.dll and most likely at the default path.
        if (il2cppBytes == metadataBytes)
        {
            logger.LogWarning("global-metadata.dat is not embedded in GameAssembly.dll.");
            if (File.Exists(metadataPath))
            {
                logger.LogWarning("Found global-metadata.dat at the default path, using it instead.");
                metadataBytes = File.ReadAllBytes(metadataPath);
            }
            else
            {
                logger.LogWarning("global-meatadata.dat is not found at the default location. " +
                    "It may be hidden somewhere else. " +
                    "\n Input the file path: (Example: C:\\Users\\_\\{YourGame}\\fake-global-metadata-name.fakeExtension", null);
                metadataPath = Path.Combine(Console.ReadLine() ?? string.Empty);
                metadataBytes = File.ReadAllBytes(metadataPath);
            }
        }
    }

    public const uint PAGE_EXECUTE_READWRITE = 0x40;

    public struct SignatureDefinition
    {
        public string pattern;
        public string mask;
        public int offset;
        public bool xref;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public IntPtr RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }
}
