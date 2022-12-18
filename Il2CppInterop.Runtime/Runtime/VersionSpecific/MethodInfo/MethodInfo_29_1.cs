using System;
using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.MethodInfo
{
    [ApplicableToUnityVersionsSince("2022.1.0")]
    public unsafe class NativeMethodInfoStructHandler_29_1 : INativeMethodInfoStructHandler
    {
        public int Size() => sizeof(Il2CppMethodInfo_29_1);
        public INativeMethodInfoStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppMethodInfo_29_1* _ = (Il2CppMethodInfo_29_1*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeMethodInfoStruct Wrap(Il2CppMethodInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppMethodInfo_29_1
        {
            public void* methodPointer;
            public void* virtualMethodPointer;
            public void* invoker_method;
            public byte* name;
            public Il2CppClass* klass;
            public Il2CppTypeStruct* return_type;
            public Il2CppTypeStruct** parameters;
            public void* runtime_data;
            public void* generic_data;
            public uint token;
            public ushort flags;
            public ushort iflags;
            public ushort slot;
            public byte parameters_count;
            public Bitfield0 _bitfield0;
            internal enum Bitfield0 : byte
            {
                BIT_is_generic = 0,
                is_generic = (1 << BIT_is_generic),
                BIT_is_inflated = 1,
                is_inflated = (1 << BIT_is_inflated),
                BIT_wrapper_type = 2,
                wrapper_type = (1 << BIT_wrapper_type),
                BIT_has_full_generic_sharing_signature = 3,
                has_full_generic_sharing_signature = (1 << BIT_has_full_generic_sharing_signature),
            }

        }

        internal class NativeStructWrapper : INativeMethodInfoStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            private static int _bitfield0offset = Marshal.OffsetOf<Il2CppMethodInfo_29_1>(nameof(Il2CppMethodInfo_29_1._bitfield0)).ToInt32();
            public IntPtr Pointer { get; }
            private Il2CppMethodInfo_29_1* _ => (Il2CppMethodInfo_29_1*)Pointer;
            public Il2CppMethodInfo* MethodInfoPointer => (Il2CppMethodInfo*)Pointer;
            public ref IntPtr Name => ref *(IntPtr*)&_->name;
            public ref ushort Slot => ref _->slot;
            public ref IntPtr MethodPointer => ref *(IntPtr*)&_->methodPointer;
            public ref Il2CppClass* Class => ref _->klass;
            public ref IntPtr InvokerMethod => ref *(IntPtr*)&_->invoker_method;
            public ref Il2CppTypeStruct* ReturnType => ref _->return_type;
            public ref Il2CppMethodFlags Flags => ref *(Il2CppMethodFlags*)&_->flags;
            public ref byte ParametersCount => ref _->parameters_count;
            public ref Il2CppParameterInfo* Parameters => ref *(Il2CppParameterInfo**)&_->parameters;
            public ref uint Token => ref _->token;
            public bool IsGeneric
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppMethodInfo_29_1.Bitfield0.BIT_is_generic);
                set => this.SetBit(_bitfield0offset, (int)Il2CppMethodInfo_29_1.Bitfield0.BIT_is_generic, value);
            }
            public bool IsInflated
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppMethodInfo_29_1.Bitfield0.BIT_is_inflated);
                set => this.SetBit(_bitfield0offset, (int)Il2CppMethodInfo_29_1.Bitfield0.BIT_is_inflated, value);
            }
            public bool IsMarshalledFromNative
            {
                get => false;
                set { }
            }
        }

    }

}
