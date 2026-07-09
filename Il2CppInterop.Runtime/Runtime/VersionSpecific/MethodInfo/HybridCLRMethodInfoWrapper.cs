using System;
using Il2CppInterop.Runtime.Injection;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo;

/// <summary>
/// Wraps an existing INativeMethodInfoStruct to provide access to HybridCLR-specific fields.
/// This wrapper dynamically calculates field offsets based on the base MethodInfo size.
///
/// HybridCLR has two different MethodInfo extension layouts:
///
/// NEW layout (default, newer il2cpp_plus branches):
///   - initInterpCallMethodPointer: bit 5 of _bitfield0 (last byte of standard MethodInfo)
///   - isInterpterImpl: bit 6 of _bitfield0
///   - void* interpData (at baseSize)
///   - void* methodPointerCallByInterp
///   - void* virtualMethodPointerCallByInterp
///
/// LEGACY layout (older il2cpp_plus branches, set HybridCLRCompat.UseLegacyMethodInfoLayout = true):
///   - void* interpData (at baseSize)
///   - void* methodPointerCallByInterp
///   - void* virtualMethodPointerCallByInterp
///   - bool initInterpCallMethodPointer (1 byte)
///   - bool isInterpterImpl (1 byte)
/// </summary>
public unsafe class HybridCLRMethodInfoWrapper : IHybridCLRMethodInfoStruct
{
    private readonly INativeMethodInfoStruct _inner;
    private readonly int _baseSize;

    // Bit positions within _bitfield0 (the last byte of standard MethodInfo) - NEW layout only
    private const byte InitInterpCallMethodPointerBit = 0x20; // bit 5
    private const byte IsInterpterImplBit = 0x40; // bit 6

    /// <summary>
    /// Creates a HybridCLR wrapper around an existing MethodInfo struct.
    /// </summary>
    /// <param name="inner">The base MethodInfo struct to wrap.</param>
    /// <param name="baseSize">The size of the base MethodInfo struct (without HybridCLR extensions).</param>
    public HybridCLRMethodInfoWrapper(INativeMethodInfoStruct inner, int baseSize)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _baseSize = baseSize;
    }

    private bool UseLegacyLayout => HybridCLRCompat.UseLegacyMethodInfoLayout;

    // Delegate all standard INativeMethodInfoStruct properties to inner
    public IntPtr Pointer => _inner.Pointer;
    public Il2CppMethodInfo* MethodInfoPointer => _inner.MethodInfoPointer;
    public ref IntPtr Name => ref _inner.Name;
    public ref ushort Slot => ref _inner.Slot;
    public ref IntPtr MethodPointer => ref _inner.MethodPointer;
    public ref IntPtr VirtualMethodPointer => ref _inner.VirtualMethodPointer;
    public ref Il2CppClass* Class => ref _inner.Class;
    public ref IntPtr InvokerMethod => ref _inner.InvokerMethod;
    public ref Il2CppTypeStruct* ReturnType => ref _inner.ReturnType;
    public ref Il2CppMethodFlags Flags => ref _inner.Flags;
    public ref byte ParametersCount => ref _inner.ParametersCount;
    public ref Il2CppParameterInfo* Parameters => ref _inner.Parameters;
    public ref uint Token => ref _inner.Token;
    public bool IsGeneric { get => _inner.IsGeneric; set => _inner.IsGeneric = value; }
    public bool IsInflated { get => _inner.IsInflated; set => _inner.IsInflated = value; }
    public bool IsMarshalledFromNative { get => _inner.IsMarshalledFromNative; set => _inner.IsMarshalledFromNative = value; }

    // HybridCLR-specific fields

    // _bitfield0 is right after parameters_count in the MethodInfo struct.
    // We MUST use ParametersCount address to find it, because MethodSize() includes padding.
    // MethodSize() = 0x58 (with padding), but actual bitfield0 is at 0x53.
    private byte* Bitfield0Ptr
    {
        get
        {
            fixed (byte* pCount = &_inner.ParametersCount)
            {
                return pCount + 1; // _bitfield0 is right after parameters_count
            }
        }
    }

    // HybridCLR extension fields start at MethodSize() (after padding).
    // This is correct because HybridCLR adds fields after the standard struct.
    private int ExtensionFieldsStart => _baseSize;

    private IntPtr* InterpDataPtr => (IntPtr*)((byte*)Pointer + ExtensionFieldsStart);
    private IntPtr* MethodPointerCallByInterpPtr => (IntPtr*)((byte*)Pointer + ExtensionFieldsStart + IntPtr.Size);
    private IntPtr* VirtualMethodPointerCallByInterpPtr => (IntPtr*)((byte*)Pointer + ExtensionFieldsStart + IntPtr.Size * 2);

    // LEGACY layout: bool fields are after the 3 pointers
    private byte* LegacyInitInterpCallMethodPointerPtr => (byte*)Pointer + ExtensionFieldsStart + IntPtr.Size * 3;
    private byte* LegacyIsInterpterImplPtr => (byte*)Pointer + ExtensionFieldsStart + IntPtr.Size * 3 + 1;

    public bool IsInterpterImpl
    {
        get
        {
            if (UseLegacyLayout)
                return *LegacyIsInterpterImplPtr != 0;
            return (*Bitfield0Ptr & IsInterpterImplBit) != 0;
        }
        set
        {
            if (UseLegacyLayout)
            {
                *LegacyIsInterpterImplPtr = value ? (byte)1 : (byte)0;
            }
            else
            {
                if (value)
                    *Bitfield0Ptr |= IsInterpterImplBit;
                else
                    *Bitfield0Ptr = (byte)(*Bitfield0Ptr & ~IsInterpterImplBit);
            }
        }
    }

    public bool InitInterpCallMethodPointer
    {
        get
        {
            if (UseLegacyLayout)
                return *LegacyInitInterpCallMethodPointerPtr != 0;
            return (*Bitfield0Ptr & InitInterpCallMethodPointerBit) != 0;
        }
        set
        {
            if (UseLegacyLayout)
            {
                *LegacyInitInterpCallMethodPointerPtr = value ? (byte)1 : (byte)0;
            }
            else
            {
                if (value)
                    *Bitfield0Ptr |= InitInterpCallMethodPointerBit;
                else
                    *Bitfield0Ptr = (byte)(*Bitfield0Ptr & ~InitInterpCallMethodPointerBit);
            }
        }
    }

    public ref IntPtr InterpData => ref *InterpDataPtr;
    public ref IntPtr MethodPointerCallByInterp => ref *MethodPointerCallByInterpPtr;
    public ref IntPtr VirtualMethodPointerCallByInterp => ref *VirtualMethodPointerCallByInterpPtr;
}
