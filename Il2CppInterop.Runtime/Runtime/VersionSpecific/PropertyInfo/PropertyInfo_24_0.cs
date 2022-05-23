using System;
using System.Runtime.InteropServices;

namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.PropertyInfo;

[ApplicableToUnityVersionsSince("2018.3.0")]
public unsafe class NativePropertyInfoStructHandler_24_0 : INativePropertyInfoStructHandler
{
    public int Size()
    {
        return sizeof(Il2CppPropertyInfo_24_0);
    }

    public INativePropertyInfoStruct CreateNewStruct()
    {
        var ptr = Marshal.AllocHGlobal(Size());
        var _ = (Il2CppPropertyInfo_24_0*)ptr;
        *_ = default;
        return new NativeStructWrapper(ptr);
    }

    public INativePropertyInfoStruct Wrap(Il2CppPropertyInfo* ptr)
    {
        if (ptr == null) return null;
        return new NativeStructWrapper((IntPtr)ptr);
    }

    internal struct Il2CppPropertyInfo_24_0
    {
        public Il2CppClass* parent;
        public byte* name;
        public Il2CppMethodInfo* get;
        public Il2CppMethodInfo* set;
        public uint attrs;
        public uint token;
    }

    internal class NativeStructWrapper : INativePropertyInfoStruct
    {
        public NativeStructWrapper(IntPtr ptr)
        {
            Pointer = ptr;
        }

        private Il2CppPropertyInfo_24_0* _ => (Il2CppPropertyInfo_24_0*)Pointer;
        public IntPtr Pointer { get; }
        public Il2CppPropertyInfo* PropertyInfoPointer => (Il2CppPropertyInfo*)Pointer;
        public ref IntPtr Name => ref *(IntPtr*)&_->name;
        public ref Il2CppClass* Parent => ref _->parent;
        public ref Il2CppMethodInfo* Get => ref _->get;
        public ref Il2CppMethodInfo* Set => ref _->set;
        public ref uint Attrs => ref _->attrs;
    }
}
