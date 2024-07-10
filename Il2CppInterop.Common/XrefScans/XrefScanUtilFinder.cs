using Iced.Intel;

namespace Il2CppInterop.Common.XrefScans;

internal static class XrefScanUtilFinder
{
    public static IEnumerable<IntPtr> FindLastRcxReadAddressesBeforeCallTo(IntPtr codeStart, IntPtr callTarget)
    {
        var decoder = XrefScanner.DecoderForAddress(codeStart);
        var readList = new List<IntPtr>();
        var lastRcxRead = IntPtr.Zero;

        while (true)
        {
            decoder.Decode(out var instruction);
            if (decoder.LastError == DecoderError.NoMoreBytes) return readList;

            if (instruction.FlowControl == FlowControl.Return)
                return readList;

            if (instruction.FlowControl == FlowControl.UnconditionalBranch)
                continue;

            if (instruction.Mnemonic is Mnemonic.Int or Mnemonic.Int1 or Mnemonic.Int3)
                return readList;

            if (instruction.Mnemonic == Mnemonic.Call)
            {
                var target = ExtractTargetAddress(instruction);
                if ((IntPtr)target == callTarget)
                {
                    readList.Add(lastRcxRead);
                }
            }

            if (instruction.Mnemonic == Mnemonic.Lea && instruction.Op0Register == Register.RCX &&
                instruction.MemoryBase == Register.RIP && instruction.IsIPRelativeMemoryOperand)
            {
                lastRcxRead = (IntPtr)instruction.IPRelativeMemoryAddress;
            }
        }
    }

    public static IntPtr FindByteWriteTargetRightAfterCallTo(IntPtr codeStart, IntPtr callTarget)
    {
        var decoder = XrefScanner.DecoderForAddress(codeStart);
        var seenCall = false;

        while (true)
        {
            decoder.Decode(out var instruction);
            if (decoder.LastError == DecoderError.NoMoreBytes) return IntPtr.Zero;

            if (instruction.FlowControl == FlowControl.Return)
                return IntPtr.Zero;

            if (instruction.FlowControl == FlowControl.UnconditionalBranch)
                continue;

            if (instruction.Mnemonic is Mnemonic.Int or Mnemonic.Int1 or Mnemonic.Int3)
                return IntPtr.Zero;

            if (instruction.Mnemonic == Mnemonic.Call)
            {
                var target = ExtractTargetAddress(instruction);
                if ((IntPtr)target == callTarget)
                    seenCall = true;
            }

            if (instruction.Mnemonic == Mnemonic.Mov && seenCall)
                if (instruction.Op0Kind == OpKind.Memory && (instruction.MemorySize == MemorySize.Int8 ||
                                                             instruction.MemorySize == MemorySize.UInt8))
                    return (IntPtr)instruction.IPRelativeMemoryAddress;
        }
    }

    private static ulong ExtractTargetAddress(in Instruction instruction)
    {
        switch (instruction.Op0Kind)
        {
            case OpKind.NearBranch16:
                return instruction.NearBranch16;
            case OpKind.NearBranch32:
                return instruction.NearBranch32;
            case OpKind.NearBranch64:
                return instruction.NearBranch64;
            case OpKind.FarBranch16:
                return instruction.FarBranch16;
            case OpKind.FarBranch32:
                return instruction.FarBranch32;
            default:
                return 0;
        }
    }
}
