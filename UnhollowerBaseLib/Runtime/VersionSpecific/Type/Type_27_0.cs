using System;
using System.Runtime.InteropServices;
namespace UnhollowerBaseLib.Runtime.VersionSpecific.Type
{
    [ApplicableToUnityVersionsSince("2021.1.0")]
    public unsafe class NativeTypeStructHandler_27_0 : INativeTypeStructHandler
    {
        public int Size() => sizeof(Il2CppType_27_0);
        public INativeTypeStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppType_27_0* _ = (Il2CppType_27_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeTypeStruct Wrap(Il2CppTypeStruct* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppType_27_0
        {
            public void* data;
            public ushort attrs;
            public byte type;
            public Bitfield0 _bitfield0;
            internal enum Bitfield0 : byte
            {
                BIT_byref = 5,
                byref = (1 << BIT_byref),
                BIT_pinned = 6,
                pinned = (1 << BIT_pinned),
                BIT_valuetype = 7,
                valuetype = (1 << BIT_valuetype),
            }

        }

        internal class NativeStructWrapper : INativeTypeStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            private static int _bitfield0offset = Marshal.OffsetOf<Il2CppType_27_0>(nameof(Il2CppType_27_0._bitfield0)).ToInt32();
            public IntPtr Pointer { get; }
            private Il2CppType_27_0* _ => (Il2CppType_27_0*)Pointer;
            public Il2CppTypeStruct* TypePointer => (Il2CppTypeStruct*)Pointer;
            public ref IntPtr Data => ref *(IntPtr*)&_->data;
            public ref ushort Attrs => ref _->attrs;
            public ref Il2CppTypeEnum Type => ref *(Il2CppTypeEnum*)&_->type;
            public bool ByRef
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppType_27_0.Bitfield0.BIT_byref);
                set => this.SetBit(_bitfield0offset, (int)Il2CppType_27_0.Bitfield0.BIT_byref, value);
            }
            public bool Pinned
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppType_27_0.Bitfield0.BIT_pinned);
                set => this.SetBit(_bitfield0offset, (int)Il2CppType_27_0.Bitfield0.BIT_pinned, value);
            }
            public bool ValueType
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppType_27_0.Bitfield0.BIT_valuetype);
                set => this.SetBit(_bitfield0offset, (int)Il2CppType_27_0.Bitfield0.BIT_valuetype, value);
            }
        }

    }

}
