using System;
using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.AssemblyName
{
    [ApplicableToUnityVersionsSince("5.2.2")]
    public unsafe class NativeAssemblyNameStructHandler_16_0 : INativeAssemblyNameStructHandler
    {
        public int Size() => sizeof(Il2CppAssemblyName_16_0);
        public INativeAssemblyNameStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppAssemblyName_16_0* _ = (Il2CppAssemblyName_16_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeAssemblyNameStruct Wrap(Il2CppAssemblyName* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppAssemblyName_16_0
        {
            public int nameIndex;
            public int cultureIndex;
            public int hashValueIndex;
            public int publicKeyIndex;
            public uint hash_alg;
            public int hash_len;
            public uint flags;
            public int major;
            public int minor;
            public int build;
            public int revision;
            public ulong publicKeyToken;
        }

        internal class NativeStructWrapper : INativeAssemblyNameStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppAssemblyName_16_0* _ => (Il2CppAssemblyName_16_0*)Pointer;
            public Il2CppAssemblyName* AssemblyNamePointer => (Il2CppAssemblyName*)Pointer;
            public ref IntPtr Name => ref *(IntPtr*)&_->nameIndex;
            public ref IntPtr Culture => ref *(IntPtr*)&_->cultureIndex;
            public ref IntPtr PublicKey => ref *(IntPtr*)&_->publicKeyIndex;
            public ref int Major => ref _->major;
            public ref int Minor => ref _->minor;
            public ref int Build => ref _->build;
            public ref int Revision => ref _->revision;
            public ref ulong PublicKeyToken => ref _->publicKeyToken;
        }

    }

}
