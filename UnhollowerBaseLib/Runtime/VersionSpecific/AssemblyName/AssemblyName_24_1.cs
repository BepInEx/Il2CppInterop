using System;
using System.Runtime.InteropServices;
namespace UnhollowerBaseLib.Runtime.VersionSpecific.AssemblyName
{
    [ApplicableToUnityVersionsSince("2018.4.34")]
    public unsafe class NativeAssemblyNameStructHandler_24_1 : INativeAssemblyNameStructHandler
    {
        public int Size() => sizeof(Il2CppAssemblyName_24_1);
        public INativeAssemblyNameStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppAssemblyName_24_1* _ = (Il2CppAssemblyName_24_1*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeAssemblyNameStruct Wrap(Il2CppAssemblyName* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppAssemblyName_24_1
        {
            public byte* name;
            public byte* culture;
            public byte* public_key;
            public uint hash_alg;
            public int hash_len;
            public uint flags;
            public int major;
            public int minor;
            public int build;
            public int revision;
            public ulong public_key_token;
        }

        internal class NativeStructWrapper : INativeAssemblyNameStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppAssemblyName_24_1* _ => (Il2CppAssemblyName_24_1*)Pointer;
            public Il2CppAssemblyName* AssemblyNamePointer => (Il2CppAssemblyName*)Pointer;
            public ref IntPtr Name => ref *(IntPtr*)&_->name;
            public ref IntPtr Culture => ref *(IntPtr*)&_->culture;
            public ref IntPtr PublicKey => ref *(IntPtr*)&_->public_key;
            public ref int Major => ref _->major;
            public ref int Minor => ref _->minor;
            public ref int Build => ref _->build;
            public ref int Revision => ref _->revision;
            public ref ulong PublicKeyToken => ref _->public_key_token;
        }

    }

}
