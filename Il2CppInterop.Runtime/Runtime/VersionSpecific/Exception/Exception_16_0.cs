using System;
using System.Runtime.InteropServices;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.Exception
{
    [ApplicableToUnityVersionsSince("5.2.2")]
    public unsafe class NativeExceptionStructHandler_16_0 : INativeExceptionStructHandler
    {
        public int Size() => sizeof(Il2CppException_16_0);
        public INativeExceptionStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppException_16_0* _ = (Il2CppException_16_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeExceptionStruct Wrap(Il2CppException* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppException_16_0
        {
            public Il2CppObject _object;
            public void* trace_ips;
            public Il2CppObject* inner_ex;
            public Il2CppString* message;
            public Il2CppString* help_link;
            public Il2CppString* class_name;
            public Il2CppString* stack_trace;
            public Il2CppString* remote_stack_trace;
            public int remote_stack_index;
            public int hresult;
            public Il2CppString* source;
            public Il2CppObject* _data;
        }

        internal class NativeStructWrapper : INativeExceptionStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppException_16_0* _ => (Il2CppException_16_0*)Pointer;
            public Il2CppException* ExceptionPointer => (Il2CppException*)Pointer;
            public ref Il2CppException* InnerException => ref *(Il2CppException**)&_->inner_ex;
            public ref Il2CppString* Message => ref _->message;
            public ref Il2CppString* HelpLink => ref _->help_link;
            public ref Il2CppString* ClassName => ref _->class_name;
            public ref Il2CppString* StackTrace => ref _->stack_trace;
            public ref Il2CppString* RemoteStackTrace => ref _->remote_stack_trace;
        }

    }

}
