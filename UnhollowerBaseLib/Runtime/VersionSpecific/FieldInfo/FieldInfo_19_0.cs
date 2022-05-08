using System;
using System.Runtime.InteropServices;
namespace UnhollowerBaseLib.Runtime.VersionSpecific.FieldInfo
{
    [ApplicableToUnityVersionsSince("5.3.2")]
    public unsafe class NativeFieldInfoStructHandler_19_0 : INativeFieldInfoStructHandler
    {
        public int Size() => sizeof(Il2CppFieldInfo_19_0);
        public INativeFieldInfoStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppFieldInfo_19_0* _ = (Il2CppFieldInfo_19_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeFieldInfoStruct Wrap(Il2CppFieldInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppFieldInfo_19_0
        {
            public byte* name;
            public Il2CppTypeStruct* type;
            public Il2CppClass* parent;
            public int offset;
            public int customAttributeIndex;
            public uint token;
        }

        internal class NativeStructWrapper : INativeFieldInfoStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppFieldInfo_19_0* _ => (Il2CppFieldInfo_19_0*)Pointer;
            public Il2CppFieldInfo* FieldInfoPointer => (Il2CppFieldInfo*)Pointer;
            public ref IntPtr Name => ref *(IntPtr*)&_->name;
            public ref Il2CppTypeStruct* Type => ref _->type;
            public ref Il2CppClass* Parent => ref _->parent;
            public ref int Offset => ref _->offset;
        }

    }

}
