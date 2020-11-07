using System;
using System.Runtime.InteropServices;

namespace UnhollowerBaseLib.Runtime.VersionSpecific
{
    public class Unity2019NativeClassStructHandler : INativeClassStructHandler
    {
        public unsafe Unity2019NativeClassStructHandler()
        {
            Il2CppClassU2019_32 ex = new Il2CppClassU2019_32();
            byte* addr = (byte*)&ex;
            LogSupport.Trace($"Size:                         {sizeof(Il2CppClassU2019_32)}");
            LogSupport.Trace($"klass Offset:       {(byte*)&ex.Part1.klass - addr}");
            LogSupport.Trace($"typeHierarchy Offset:       {(byte*)&ex.Part1.typeHierarchy - addr}");
            LogSupport.Trace($"unity_user_data Offset:       {(byte*)&ex.unity_user_data - addr}");
            LogSupport.Trace($"cctor_finished Offset:       {(byte*)&ex.Part2.cctor_finished - addr}");
            LogSupport.Trace($"cctor_thread Offset:       {(byte*)&ex.Part2.cctor_thread - addr}");
            LogSupport.Trace($"genericContainerIndex Offset:       {(byte*)&ex.Part2.genericContainerIndex - addr}");
            LogSupport.Trace($"field_count Offset:       {(byte*)&ex.Part2.field_count - addr}");
            LogSupport.Trace($"interface_offsets_count Offset:       {(byte*)&ex.Part2.interface_offsets_count - addr}");
            LogSupport.Trace($"typeHierarchyDepth Offset:    {&ex.typeHierarchyDepth - addr}");
            LogSupport.Trace($"genericRecursionDepth Offset: {&ex.genericRecursionDepth - addr}");
            LogSupport.Trace($"rank Offset:                  {&ex.rank - addr}");
            LogSupport.Trace($"minimumAlignment Offset:      {&ex.minimumAlignment - addr}");
            LogSupport.Trace($"naturalAligmnent Offset:       {&ex.naturalAlignment - addr}");
            LogSupport.Trace($"packingSize Offset:           {&ex.packingSize - addr}");
            LogSupport.Trace($"bitfield_1 Offset:            {(byte*)&ex.bitfield_1 - addr}");
            LogSupport.Trace($"bitfield_2 Offset:            {(byte*)&ex.bitfield_2 - addr}");
        }

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
        
        [StructLayout(LayoutKind.Sequential)]
        private struct Il2CppClassU2019_64
        {
            public Il2CppClassPart1 Part1;
            public IntPtr unity_user_data;
            public Il2CppClassPart2 Part2;
            public byte typeHierarchyDepth; // Initialized in SetupTypeHierachy
            public byte genericRecursionDepth;
            public byte rank;
            public byte minimumAlignment; // Alignment of this type
            public byte naturalAlignment; // Alignment of this type without accounting for packing
            public byte packingSize;
            public ClassBitfield1 bitfield_1;
            public ClassBitfield2 bitfield_2;
            public byte padding;
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
        
        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 192)]
        private struct Il2CppClassU2019_32
        {
            public Il2CppClassPart1 Part1;
            public int unity_user_data;
            public Il2CppClassPart2_32 Part2;
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