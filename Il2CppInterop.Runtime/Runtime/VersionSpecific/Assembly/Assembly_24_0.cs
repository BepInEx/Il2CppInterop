using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.AssemblyName;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.Assembly
{
    [ApplicableToUnityVersionsSince("2018.1.0")]
    public unsafe class NativeAssemblyStructHandler_24_0 : INativeAssemblyStructHandler
    {
        public int Size() => sizeof(Il2CppAssembly_24_0);
        public INativeAssemblyStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppAssembly_24_0* _ = (Il2CppAssembly_24_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeAssemblyStruct Wrap(Il2CppAssembly* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppAssembly_24_0
        {
            public Il2CppImage* image;
            public int customAttributeIndex;
            public int referencedAssemblyStart;
            public int referencedAssemblyCount;
            public NativeAssemblyNameStructHandler_24_0.Il2CppAssemblyName_24_0 aname;
        }

        internal class NativeStructWrapper : INativeAssemblyStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppAssembly_24_0* _ => (Il2CppAssembly_24_0*)Pointer;
            public Il2CppAssembly* AssemblyPointer => (Il2CppAssembly*)Pointer;
            public INativeAssemblyNameStruct Name
            {
                get => UnityVersionHandler.Wrap((Il2CppAssemblyName*)&_->aname);
                set => _->aname = *(NativeAssemblyNameStructHandler_24_0.Il2CppAssemblyName_24_0*)Name.AssemblyNamePointer;
            }
            public ref Il2CppImage* Image => ref _->image;
        }

    }

}
