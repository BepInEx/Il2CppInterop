using System;

namespace UnhollowerBaseLib.Runtime.VersionSpecific.Exception
{
    public interface INativeExceptionStructHandler : INativeStructHandler
    {
        INativeExceptionStruct CreateNewStruct();
        unsafe INativeExceptionStruct Wrap(Il2CppException* exceptionPointer);
    }

    public interface INativeExceptionStruct : INativeStruct
    {
        unsafe Il2CppException* ExceptionPointer { get; }

        unsafe ref Il2CppException* InnerException { get; }

        unsafe ref Il2CppString* Message { get; }

        unsafe ref Il2CppString* HelpLink { get; }

        unsafe ref Il2CppString* ClassName { get; }

        unsafe ref Il2CppString* StackTrace { get; }

        unsafe ref Il2CppString* RemoteStackTrace { get; }
    }
}
