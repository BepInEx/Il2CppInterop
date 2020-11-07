using System;
using System.Runtime.InteropServices;

namespace UnhollowerBaseLib.Runtime.VersionSpecific
{
    public class Unity2018_4NativeClassStructHandler : INativeClassStructHandler
    {
	    public unsafe Unity2018_4NativeClassStructHandler()
	    {
		    Il2CppClassU2018_4 ex = new Il2CppClassU2018_4();
		    byte* addr = (byte*)&ex;
		    LogSupport.Trace($"Size:                         {sizeof(Il2CppClassU2018_4)}");
		    LogSupport.Trace($"typeHierarchyDepth Offset:    {&ex.typeHierarchyDepth - addr}");
		    LogSupport.Trace($"genericRecursionDepth Offset: {&ex.genericRecursionDepth - addr}");
		    LogSupport.Trace($"rank Offset:                  {&ex.rank - addr}");
		    LogSupport.Trace($"minimumAlignment Offset:      {&ex.minimumAlignment - addr}");
		    LogSupport.Trace($"naturalAlignment Offset:       {&ex.naturalAlignment - addr}");
		    LogSupport.Trace($"packingSize Offset:           {&ex.packingSize - addr}");
		    LogSupport.Trace($"bitfield_1 Offset:            {(byte*)&ex.bitfield_1 - addr}");
		    LogSupport.Trace($"bitfield_2 Offset:            {(byte*)&ex.bitfield_2 - addr}");
	    }

        public unsafe INativeClassStruct CreateNewClassStruct(int vTableSlots)
        {
            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Il2CppClassU2018_4>() + Marshal.SizeOf<VirtualInvokeData>() * vTableSlots);

            *(Il2CppClassU2018_4*) pointer = default;
            
            return new Unity2018_4NativeClassStruct(pointer);
        }

        public unsafe INativeClassStruct Wrap(Il2CppClass* classPointer)
        {
            return new Unity2018_4NativeClassStruct((IntPtr) classPointer);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Il2CppClassU2018_4
        {
            public Il2CppClassPart1 Part1;
            public Il2CppClassPart2 Part2;
            public byte typeHierarchyDepth; // Initialized in SetupTypeHierachy
            public byte genericRecursionDepth;
            public byte rank;
            public byte minimumAlignment; // Alignment of this type
            public byte naturalAlignment; // Alignment of this type without accounting for packing
            public byte packingSize;
            public ClassBitfield1 bitfield_1;
            public ClassBitfield2 bitfield_2;
        }

        private unsafe class Unity2018_4NativeClassStruct : INativeClassStruct
        {
            public Unity2018_4NativeClassStruct(IntPtr pointer)
            {
                Pointer = pointer;
            }

            public IntPtr Pointer { get; }
            public Il2CppClass* ClassPointer => (Il2CppClass*) Pointer;

            public IntPtr VTable => IntPtr.Add(Pointer, Marshal.SizeOf<Il2CppClassU2018_4>());

            private Il2CppClassU2018_4* Instance => (Il2CppClassU2018_4*)Pointer;

            public Il2CppClassPart1* Part1 => &Instance->Part1;
            public uint* instance_size => &Instance->Part2.instance_size;
            public ushort* vtable_count => &Instance->Part2.vtable_count;
            public int* native_size => &Instance->Part2.native_size;
            public uint* actualSize => &Instance->Part2.actualSize;
            public ushort* method_count => &Instance->Part2.method_count;
            public Il2CppClassAttributes* flags => &Instance->Part2.flags;
            public ClassBitfield1* Bitfield1 => &((Il2CppClassU2018_4*)Pointer)->bitfield_1;
            public ClassBitfield2* Bitfield2 => &((Il2CppClassU2018_4*)Pointer)->bitfield_2;
        }
    }
}