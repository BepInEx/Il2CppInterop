using System;
using System.Runtime.InteropServices;
using UnhollowerBaseLib.Runtime.VersionSpecific.AssemblyName;
namespace UnhollowerBaseLib.Runtime.VersionSpecific.Assembly
{
    [ApplicableToUnityVersionsSince("2018.4.34")]
    public unsafe class NativeAssemblyStructHandler_24_2 : INativeAssemblyStructHandler
    {
        public int Size() => sizeof(Il2CppAssembly_24_2);
        public INativeAssemblyStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppAssembly_24_2* _ = (Il2CppAssembly_24_2*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeAssemblyStruct Wrap(Il2CppAssembly* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppAssembly_24_2
        {
            public Il2CppImage* image;
            public uint token;
            public int referencedAssemblyStart;
            public int referencedAssemblyCount;
            public NativeAssemblyNameStructHandler_24_1.Il2CppAssemblyName_24_1 aname;
        }

        internal class NativeStructWrapper : INativeAssemblyStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppAssembly_24_2* _ => (Il2CppAssembly_24_2*)Pointer;
            public Il2CppAssembly* AssemblyPointer => (Il2CppAssembly*)Pointer;
            public INativeAssemblyNameStruct Name
            {
                get => UnityVersionHandler.Wrap((Il2CppAssemblyName*)&_->aname);
                set => _->aname = *(NativeAssemblyNameStructHandler_24_1.Il2CppAssemblyName_24_1*)Name.AssemblyNamePointer;
            }
            public ref Il2CppImage* Image => ref _->image;
        }

    }

}
