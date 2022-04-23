using System;
using System.Diagnostics;
using System.Linq;
using Il2CppInterop.Runtime.XrefScans;

namespace Il2CppInterop.Runtime
{
    internal class MemoryUtils
    {
        public struct SignatureDefinition
        {
            public string pattern;
            public string mask;
            public int offset;
            public bool xref;
        }
        public static unsafe nint FindSignatureInModule(ProcessModule module, SignatureDefinition sigDef)
        {
            nint ptr = FindSignatureInBlock(
                module.BaseAddress,
                module.ModuleMemorySize,
                sigDef.pattern,
                sigDef.mask,
                sigDef.offset
            );
            if (ptr != 0 && sigDef.xref)
                ptr = XrefScannerLowLevel.JumpTargets(ptr).FirstOrDefault();
            return ptr;
        }

        public static unsafe nint FindSignatureInBlock(nint block, long blockSize, string pattern, string mask, long sigOffset = 0)
            => FindSignatureInBlock(block, blockSize, pattern.ToCharArray(), mask.ToCharArray(), sigOffset);
        public static unsafe nint FindSignatureInBlock(nint block, long blockSize, char[] pattern, char[] mask, long sigOffset = 0)
        {
            for (long address = 0; address < blockSize; address++)
            {
                bool found = true;
                for (uint offset = 0; offset < mask.Length; offset++)
                {
                    if (((*(byte*)(address + block + offset)) != (byte)pattern[offset]) && mask[offset] != '?')
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return (nint)(address + block + sigOffset);
            }
            return 0;
        }
    }
}
