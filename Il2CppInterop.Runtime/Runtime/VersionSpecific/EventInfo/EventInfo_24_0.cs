using System;
using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.EventInfo
{
    [ApplicableToUnityVersionsSince("2018.3.0")]
    public unsafe class NativeEventInfoStructHandler_24_0 : INativeEventInfoStructHandler
    {
        public int Size() => sizeof(Il2CppEventInfo_24_0);
        public INativeEventInfoStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppEventInfo_24_0* _ = (Il2CppEventInfo_24_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeEventInfoStruct Wrap(Il2CppEventInfo* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppEventInfo_24_0
        {
            public byte* name;
            public Il2CppTypeStruct* eventType;
            public Il2CppClass* parent;
            public Il2CppMethodInfo* add;
            public Il2CppMethodInfo* remove;
            public Il2CppMethodInfo* raise;
            public uint token;
        }

        internal class NativeStructWrapper : INativeEventInfoStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppEventInfo_24_0* _ => (Il2CppEventInfo_24_0*)Pointer;
            public Il2CppEventInfo* EventInfoPointer => (Il2CppEventInfo*)Pointer;
            public ref IntPtr Name => ref *(IntPtr*)&_->name;
            public ref Il2CppTypeStruct* EventType => ref _->eventType;
            public ref Il2CppClass* Parent => ref _->parent;
            public ref Il2CppMethodInfo* Add => ref _->add;
            public ref Il2CppMethodInfo* Remove => ref _->remove;
            public ref Il2CppMethodInfo* Raise => ref _->raise;
        }

    }

}
