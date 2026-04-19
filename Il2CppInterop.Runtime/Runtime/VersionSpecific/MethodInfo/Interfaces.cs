using System;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;

public interface INativeMethodInfoStructHandler : INativeStructHandler
{
    INativeMethodInfoStruct CreateNewStruct();
    unsafe INativeMethodInfoStruct Wrap(Il2CppMethodInfo* methodPointer);
}

public interface INativeMethodInfoStruct : INativeStruct
{
    unsafe Il2CppMethodInfo* MethodInfoPointer { get; }
    ref IntPtr Name { get; }
    ref ushort Slot { get; }
    ref IntPtr MethodPointer { get; }

    ref IntPtr VirtualMethodPointer { get; }
    unsafe ref Il2CppClass* Class { get; }
    ref IntPtr InvokerMethod { get; }
    unsafe ref Il2CppTypeStruct* ReturnType { get; }
    ref Il2CppMethodFlags Flags { get; }
    ref byte ParametersCount { get; }
    unsafe ref Il2CppParameterInfo* Parameters { get; }
    ref uint Token { get; }
    bool IsGeneric { get; set; }
    bool IsInflated { get; set; }
    bool IsMarshalledFromNative { get; set; }
}

/// <summary>
/// Extended interface for HybridCLR-modified MethodInfo structures.
/// HybridCLR adds additional fields after the standard MethodInfo for interpreter support.
///
/// There are two different layouts depending on HybridCLR version:
///
/// NEW layout (default): bool fields are stored as bit flags in _bitfield0
///   - IsInterpterImpl: bit 6 of _bitfield0
///   - InitInterpCallMethodPointer: bit 5 of _bitfield0
///   - Then: interpData, methodPointerCallByInterp, virtualMethodPointerCallByInterp
///
/// LEGACY layout (set HybridCLRCompat.UseLegacyMethodInfoLayout = true):
///   - interpData, methodPointerCallByInterp, virtualMethodPointerCallByInterp
///   - Then: initInterpCallMethodPointer (1 byte), isInterpterImpl (1 byte)
/// </summary>
public interface IHybridCLRMethodInfoStruct : INativeMethodInfoStruct
{
    /// <summary>
    /// Whether this method is implemented by the HybridCLR interpreter.
    /// Location depends on layout: bit 6 of _bitfield0 (new) or byte after pointers (legacy).
    /// </summary>
    bool IsInterpterImpl { get; set; }

    /// <summary>
    /// Whether the interpreter call method pointer has been initialized.
    /// Location depends on layout: bit 5 of _bitfield0 (new) or byte after pointers (legacy).
    /// </summary>
    bool InitInterpCallMethodPointer { get; set; }

    /// <summary>
    /// Interpreter-specific data pointer.
    /// </summary>
    ref IntPtr InterpData { get; }

    /// <summary>
    /// Method pointer used when calling from interpreter context.
    /// </summary>
    ref IntPtr MethodPointerCallByInterp { get; }

    /// <summary>
    /// Virtual method pointer used when calling from interpreter context.
    /// </summary>
    ref IntPtr VirtualMethodPointerCallByInterp { get; }
}
