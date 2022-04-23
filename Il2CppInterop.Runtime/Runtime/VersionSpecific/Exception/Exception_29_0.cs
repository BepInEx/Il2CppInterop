using System;
using System.Runtime.InteropServices;
namespace UnhollowerBaseLib.Runtime.VersionSpecific.Exception
{
    [ApplicableToUnityVersionsSince("2021.2.0")]
    public unsafe class NativeExceptionStructHandler_29_0 : INativeExceptionStructHandler
    {
        public int Size() => sizeof(Il2CppException_29_0);
        public INativeExceptionStruct CreateNewStruct()
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size());
            Il2CppException_29_0* _ = (Il2CppException_29_0*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeExceptionStruct Wrap(Il2CppException* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppException_29_0
        {
            public Il2CppObject _object;
            public Il2CppString* className;
            public Il2CppString* message;
            public Il2CppObject* _data;
            public Il2CppException* inner_ex;
            public Il2CppString* _helpURL;
            public void* trace_ips;
            public Il2CppString* stack_trace;
            public Il2CppString* remote_stack_trace;
            public int remote_stack_index;
            public Il2CppObject* _dynamicMethods;
            public il2cpp_hresult_t hresult;
            public Il2CppString* source;
            public Il2CppObject* safeSerializationManager;
            public void* captured_traces;
            public void* native_trace_ips;
            public int caught_in_unmanaged;
        }

        internal class NativeStructWrapper : INativeExceptionStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            public IntPtr Pointer { get; }
            private Il2CppException_29_0* _ => (Il2CppException_29_0*)Pointer;
            public Il2CppException* ExceptionPointer => (Il2CppException*)Pointer;
            public ref Il2CppException* InnerException => ref _->inner_ex;
            public ref Il2CppString* Message => ref _->message;
            public ref Il2CppString* HelpLink => ref _->_helpURL;
            public ref Il2CppString* ClassName => ref _->className;
            public ref Il2CppString* StackTrace => ref _->stack_trace;
            public ref Il2CppString* RemoteStackTrace => ref _->remote_stack_trace;
        }

    }

}
