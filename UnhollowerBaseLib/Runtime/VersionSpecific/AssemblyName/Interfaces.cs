using System;

namespace UnhollowerBaseLib.Runtime.VersionSpecific.AssemblyName
{
    public interface INativeAssemblyNameStructHandler : INativeStructHandler
    {
        INativeAssemblyNameStruct CreateNewStruct();
        unsafe INativeAssemblyNameStruct Wrap(Il2CppAssemblyName* assemblyNamePointer);
    }
    public interface INativeAssemblyNameStruct : INativeStruct
    {
        unsafe Il2CppAssemblyName* AssemblyNamePointer { get; }
        ref IntPtr Name { get; }
        ref IntPtr Culture { get; }
        ref IntPtr PublicKey { get; }
        ref int Major { get; }
        ref int Minor { get; }
        ref int Build { get; }
        ref int Revision { get; }
        ref ulong PublicKeyToken { get; }
    }
}
