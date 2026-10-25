using System;
using System.Collections.Generic;
using System.IO;
using Iced.Intel;

namespace Il2CppInterop.Runtime.Injection;

internal static class XrefScanner
{
    public static IEnumerable<IntPtr> JumpTargets(IntPtr codeStart, bool ignoreReturn = false)
    {
        return JumpTargetsImpl(DecoderForAddress(codeStart), ignoreReturn);
    }

    private static IEnumerable<IntPtr> JumpTargetsImpl(Decoder myDecoder, bool ignoreReturn)
    {
        var firstFlowControl = true;

        while (true)
        {
            myDecoder.Decode(out var instruction);
            if (myDecoder.LastError == DecoderError.NoMoreBytes) yield break;

            // 0xcc - padding after most functions
            if (instruction.Mnemonic == Mnemonic.Int3)
                yield break;

            if (instruction.FlowControl == FlowControl.Return && !ignoreReturn)
                yield break;

            if (instruction.FlowControl == FlowControl.UnconditionalBranch ||
                instruction.FlowControl == FlowControl.Call)
            {
                // We hope and pray that the compiler didn't use short jumps for any function calls
                if (!instruction.IsJmpShort)
                {
                    var targetAddress = (IntPtr)ExtractTargetAddress(in instruction);
                    if (targetAddress != IntPtr.Zero)
                        yield return targetAddress;
                    if (firstFlowControl && instruction.FlowControl == FlowControl.UnconditionalBranch)
                        yield break;
                }
            }

            if (instruction.FlowControl != FlowControl.Next)
            {
                firstFlowControl = false;
            }
        }
    }

    public static IEnumerable<IntPtr> CallAndIndirectTargets(IntPtr pointer)
    {
        return CallAndIndirectTargetsImpl(DecoderForAddress(pointer, 1024 * 1024));
    }

    private static IEnumerable<IntPtr> CallAndIndirectTargetsImpl(Decoder decoder)
    {
        while (true)
        {
            decoder.Decode(out var instruction);
            if (decoder.LastError == DecoderError.NoMoreBytes) yield break;

            if (instruction.FlowControl == FlowControl.Return)
                yield break;

            if (instruction.Mnemonic == Mnemonic.Int || instruction.Mnemonic == Mnemonic.Int1)
                yield break;

            if (instruction.Mnemonic == Mnemonic.Call || instruction.Mnemonic == Mnemonic.Jmp)
            {
                var targetAddress = ExtractTargetAddress(instruction);
                if (targetAddress != 0)
                    yield return (IntPtr)targetAddress;
                continue;
            }

            if (instruction.Mnemonic == Mnemonic.Lea)
                if (instruction.MemoryBase == Register.RIP)
                {
                    var targetAddress = instruction.IPRelativeMemoryAddress;
                    if (targetAddress != 0)
                        yield return (IntPtr)targetAddress;
                }
        }
    }

    private static ulong ExtractTargetAddress(in Instruction instruction) => instruction.Op0Kind switch
    {
        OpKind.NearBranch16 => instruction.NearBranch16,
        OpKind.NearBranch32 => instruction.NearBranch32,
        OpKind.NearBranch64 => instruction.NearBranch64,
        OpKind.FarBranch16 => instruction.FarBranch16,
        OpKind.FarBranch32 => instruction.FarBranch32,
        _ => 0,
    };

    private static unsafe Decoder DecoderForAddress(IntPtr codeStart, int lengthLimit = 1000)
    {
        if (codeStart == IntPtr.Zero)
            throw new NullReferenceException(nameof(codeStart));

        var stream = new UnmanagedMemoryStream((byte*)codeStart, lengthLimit, lengthLimit, FileAccess.Read);
        var codeReader = new StreamCodeReader(stream);
        var decoder = Decoder.Create(IntPtr.Size * 8, codeReader);
        decoder.IP = (ulong)codeStart;

        return decoder;
    }
}
