using System;

namespace Il2CppInterop.Runtime.InteropTypes;

internal interface IIl2CppByReference
{
    int ReferenceSize { get; }
    IntPtr ReferenceObjectClass { get; }
    void WriteReferenceToSpan(Span<byte> span);
    void ReadReferenceFromSpan(ReadOnlySpan<byte> span);
}
