using System;
using System.Runtime.InteropServices;

namespace UnhollowerBaseLib.Runtime.VersionSpecific
{
    public class Unity2019NativeClassStructHandler : INativeClassStructHandler
    {
        public unsafe INativeClassStruct CreateNewClassStruct(int vTableSlots)
        {
	        if (IntPtr.Size == 8)
	        {
		        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Il2CppClassU2019_64>() + Marshal.SizeOf<VirtualInvokeData>() * vTableSlots);

		        *(Il2CppClassU2019_64*)pointer = default;

		        return new Unity2019NativeClassStruct_64(pointer);
            }
	        else
	        {
		        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Il2CppClassU2019_32>() + Marshal.SizeOf<VirtualInvokeData>() * vTableSlots);

		        *(Il2CppClassU2019_32*)pointer = default;

		        return new Unity2019NativeClassStruct_32(pointer);
            }
        }

        public unsafe INativeClassStruct Wrap(Il2CppClass* classPointer)
        {
            if (IntPtr.Size == 8)
            {
	            return new Unity2019NativeClassStruct_64((IntPtr)classPointer);
            }
            else
            {
	            return new Unity2019NativeClassStruct_32((IntPtr)classPointer);
            }
        }
        
        [StructLayout(LayoutKind.Sequential, Pack = 8, Size = 304)]
        private struct Il2CppClassU2019_64
        {
            public Il2CppClassPart1 Part1;
            public IntPtr unity_user_data;
            public uint initializationExceptionGCHandle;
            public uint cctor_started;
            public uint cctor_finished;
            public /*ALIGN_TYPE(8)*/IntPtr cctor_thread;
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

        private unsafe class Unity2019NativeClassStruct_64 : INativeClassStruct
        {
            public Unity2019NativeClassStruct_64(IntPtr pointer)
            {
                Pointer = pointer;
            }

            public IntPtr Pointer { get; }
            public Il2CppClass* ClassPointer => (Il2CppClass*) Pointer;

            public IntPtr VTable => IntPtr.Add(Pointer, Marshal.SizeOf<Il2CppClassU2019_64>());

            private Il2CppClassU2019_64* Instance => (Il2CppClassU2019_64*)Pointer;

            public Il2CppClassPart1* Part1 => &Instance->Part1;
            public uint* instance_size => &Instance->Part2.instance_size;
            public ushort* vtable_count => &Instance->Part2.vtable_count;
            public int* native_size => &Instance->Part2.native_size;
            public uint* actualSize => &Instance->Part2.actualSize;
            public ushort* method_count => &Instance->Part2.method_count;
            public Il2CppClassAttributes* flags => &Instance->Part2.flags;
            public ClassBitfield1* Bitfield1 => &Instance->bitfield_1;
            public ClassBitfield2* Bitfield2 => &Instance->bitfield_2;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size =188)]
        private struct Il2CppClassU2019_32
        {
            public Il2CppClassPart1 Part1;
            public int unity_user_data;
            public uint initializationExceptionGCHandle;
            public uint cctor_started;
            public uint cctor_finished;
            public /*ALIGN_TYPE(8)*/IntPtr cctor_thread;
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

        private unsafe class Unity2019NativeClassStruct_32 : INativeClassStruct
        {
            public Unity2019NativeClassStruct_32(IntPtr pointer)
            {
                Pointer = pointer;
            }

            public IntPtr Pointer { get; }
            public Il2CppClass* ClassPointer => (Il2CppClass*) Pointer;

            public IntPtr VTable => IntPtr.Add(Pointer, Marshal.SizeOf<Il2CppClassU2019_32>());

            private Il2CppClassU2019_32* Instance => (Il2CppClassU2019_32*)Pointer;

            public Il2CppClassPart1* Part1 => &Instance->Part1;
            public uint* instance_size => &Instance->Part2.instance_size;
            public ushort* vtable_count => &Instance->Part2.vtable_count;
            public int* native_size => &Instance->Part2.native_size;
            public uint* actualSize => &Instance->Part2.actualSize;
            public ushort* method_count => &Instance->Part2.method_count;
            public Il2CppClassAttributes* flags => &Instance->Part2.flags;
            public ClassBitfield1* Bitfield1 => &Instance->bitfield_1;
            public ClassBitfield2* Bitfield2 => &Instance->bitfield_2;
        }
    }
}