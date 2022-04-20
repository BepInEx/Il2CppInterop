using System;

namespace UnhollowerBaseLib.Runtime.VersionSpecific.Type
{
    public interface INativeTypeStructHandler : INativeStructHandler
    {
        INativeTypeStruct CreateNewStruct();
        unsafe INativeTypeStruct Wrap(Il2CppTypeStruct* typePointer);
    }

    public interface INativeTypeStruct : INativeStruct
    {
        unsafe Il2CppTypeStruct* TypePointer { get; }

        ref IntPtr Data { get; }

        ref ushort Attrs { get; }

        ref Il2CppTypeEnum Type { get; }

        bool ByRef { get; set; }

        bool Pinned { get; set; }
        bool ValueType { get; set; }
    }
}
