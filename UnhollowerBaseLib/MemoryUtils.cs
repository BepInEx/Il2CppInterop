using System;
using System.Diagnostics;
using System.Linq;
using UnhollowerRuntimeLib.XrefScans;

namespace UnhollowerBaseLib
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
        public static unsafe void* FindSignatureInModule(ProcessModule module, SignatureDefinition sigDef)
        {
            void* ptr = FindSignatureInBlock(
                module.BaseAddress.ToPointer(),
                module.ModuleMemorySize,
                sigDef.pattern,
                sigDef.mask,
                sigDef.offset
            );
            if (ptr != (void*)0 && sigDef.xref)
                ptr = XrefScannerLowLevel.JumpTargets((IntPtr)ptr).FirstOrDefault().ToPointer();
            return ptr;
        }

        public static unsafe void* FindSignatureInBlock(void* block, long blockSize, string pattern, string mask, long sigOffset = 0)
            => FindSignatureInBlock(block, blockSize, pattern.ToCharArray(), mask.ToCharArray(), sigOffset);
        public static unsafe void* FindSignatureInBlock(void* block, long blockSize, char[] pattern, char[] mask, long sigOffset = 0)
        {
            for (long address = 0; address < blockSize; address++)
            {
                bool found = true;
                for (uint offset = 0; offset < mask.Length; offset++)
                {
                    if (((*(byte*)(address + (long)block + offset)) != (byte)pattern[offset]) && mask[offset] != '?')
                    {
                        found = false;
                        break;
                    }
                }
                if (found)
                    return (void*)(address + (long)block + sigOffset);
            }
            return (void*)0;
        }
    }
}
