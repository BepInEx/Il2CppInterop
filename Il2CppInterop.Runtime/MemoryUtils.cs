using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Common.XrefScans;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime;

internal class MemoryUtils
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
