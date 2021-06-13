using System;
using System.Runtime.InteropServices;

namespace UnhollowerBaseLib.Runtime.VersionSpecific.Image
{
    [ApplicableToUnityVersionsSince("2020.2.0")]
    public unsafe class NativeImageStructHandler_27 : INativeImageStructHandler
    {
        public INativeImageStruct CreateNewImageStruct()
        {
            var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<Il2CppImageU2020_2>());
            var imageMetadata =
                (Il2CppImageGlobalMetadata*) Marshal.AllocHGlobal(Marshal.SizeOf<Il2CppImageGlobalMetadata>());

            *(Il2CppImageU2020_2*) pointer = default;
            *imageMetadata = default;

            imageMetadata->image = (Il2CppImage*) pointer;
            ((Il2CppImageU2020_2*) pointer)->metadataHandle = imageMetadata;

            return new NativeImageStruct(pointer);
        }

        public INativeImageStruct Wrap(Il2CppImage* imagePointer)
        {
            return new NativeImageStruct((IntPtr)imagePointer);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Il2CppImageU2020_2
        {
            public IntPtr name;      // const char*
            public IntPtr nameNoExt; // const char*
            public Il2CppAssembly* assembly;

            public uint typeCount;

            public uint exportedTypeCount;

            public uint customAttributeCount;

            public /*Il2CppNameToTypeDefinitionIndexHashTable **/ Il2CppImageGlobalMetadata* metadataHandle;

            public /*Il2CppNameToTypeDefinitionIndexHashTable **/ IntPtr nameToClassHashTable;

            public /*Il2CppCodeGenModule*/ IntPtr codeGenModule;

            public uint token;
            public byte dynamic;
        }
        
        private struct Il2CppImageGlobalMetadata
        {
            public int typeStart;
            public int exportedTypeStart;
            public int customAttributeStart;
            public int entryPointIndex;
            public Il2CppImage* image;
        }

        private class NativeImageStruct : INativeImageStruct
        {
            public NativeImageStruct(IntPtr pointer)
            {
                Pointer = pointer;
            }

            public IntPtr Pointer { get; }

            public Il2CppImage* ImagePointer => (Il2CppImage*) Pointer;

            private Il2CppImageU2020_2* NativeImage => (Il2CppImageU2020_2*) ImagePointer;

            public ref Il2CppAssembly* Assembly => ref NativeImage->assembly;

            public ref byte Dynamic => ref NativeImage->dynamic;

            public ref IntPtr Name => ref NativeImage->name;
            
            public bool HasNameNoExt => true;

            public ref IntPtr NameNoExt => ref NativeImage->nameNoExt;
        }
    }
}