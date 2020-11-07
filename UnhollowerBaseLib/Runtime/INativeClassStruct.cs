using System;

namespace UnhollowerBaseLib.Runtime
{
    public interface INativeClassStruct
    {
        IntPtr Pointer { get; }
        unsafe Il2CppClass* ClassPointer { get; }
        IntPtr VTable { get; }

        unsafe Il2CppClassPart1* Part1 { get; }
		unsafe uint* instance_size { get; }
        unsafe ushort* vtable_count { get; }
        unsafe int* native_size { get; }
        unsafe uint* actualSize { get; }
        unsafe ushort* method_count { get; }
        unsafe Il2CppClassAttributes* flags { get; }
        unsafe ClassBitfield1* Bitfield1 { get; }
        unsafe ClassBitfield2* Bitfield2 { get; }
    }
}