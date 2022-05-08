using System;

namespace UnhollowerBaseLib.Runtime.VersionSpecific.MethodInfo
{
    public interface INativeMethodInfoStructHandler : INativeStructHandler
    {
        INativeMethodInfoStruct CreateNewStruct();
        unsafe INativeMethodInfoStruct Wrap(Il2CppMethodInfo* methodPointer);
    }


    public interface INativeMethodInfoStruct : INativeStruct
    {
        unsafe Il2CppMethodInfo* MethodInfoPointer { get; }
        ref IntPtr Name { get; }
        ref ushort Slot { get; }
        ref IntPtr MethodPointer { get; }
        unsafe ref Il2CppClass* Class { get; }
        ref IntPtr InvokerMethod { get; }
        unsafe ref Il2CppTypeStruct* ReturnType { get; }
        ref Il2CppMethodFlags Flags { get; }
        ref byte ParametersCount { get; }
        unsafe ref Il2CppParameterInfo* Parameters { get; }
        ref uint Token { get; }
        bool IsGeneric { get; set; }
        bool IsInflated { get; set; }
        bool IsMarshalledFromNative { get; set; }
    }
}