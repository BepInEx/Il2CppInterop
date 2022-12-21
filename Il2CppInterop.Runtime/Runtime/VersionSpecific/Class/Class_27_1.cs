using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Type;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.Class
{
    [ApplicableToUnityVersionsSince("2020.3.20")]
    public unsafe class NativeClassStructHandler_27_1 : INativeClassStructHandler
    {
        public int Size() => sizeof(Il2CppClass_27_1);
        public INativeClassStruct CreateNewStruct(int vTableSlots)
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size() + sizeof(VirtualInvokeData) * vTableSlots);
            Il2CppClass_27_1* _ = (Il2CppClass_27_1*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeClassStruct Wrap(Il2CppClass* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppClass_27_1
        {
            public Il2CppImage* image;
            public void* gc_desc;
            public byte* name;
            public byte* namespaze;
            public NativeTypeStructHandler_16_0.Il2CppType_16_0 byval_arg;
            public NativeTypeStructHandler_16_0.Il2CppType_16_0 this_arg;
            public Il2CppClass* element_class;
            public Il2CppClass* castClass;
            public Il2CppClass* declaringType;
            public Il2CppClass* parent;
            public void* generic_class;
            public Il2CppMetadataTypeHandle typeMetadataHandle;
            public void* interopData;
            public Il2CppClass* klass;
            public Il2CppFieldInfo* fields;
            public Il2CppEventInfo* events;
            public Il2CppPropertyInfo* properties;
            public Il2CppMethodInfo** methods;
            public Il2CppClass** nestedTypes;
            public Il2CppClass** implementedInterfaces;
            public Il2CppRuntimeInterfaceOffsetPair* interfaceOffsets;
            public void* static_fields;
            public void* rgctx_data;
            public Il2CppClass** typeHierarchy;
            public void* unity_user_data;
            public uint initializationExceptionGCHandle;
            public uint cctor_started;
            public uint cctor_finished;
            public IntPtr cctor_thread;
            public Il2CppMetadataGenericContainerHandle genericContainerHandle;
            public uint instance_size;
            public uint actualSize;
            public uint element_size;
            public int native_size;
            public uint static_fields_size;
            public uint thread_static_fields_size;
            public int thread_static_fields_offset;
            public uint flags;
            public uint token;
            public ushort method_count;
            public ushort property_count;
            public ushort field_count;
            public ushort event_count;
            public ushort nested_type_count;
            public ushort vtable_count;
            public ushort interfaces_count;
            public ushort interface_offsets_count;
            public byte typeHierarchyDepth;
            public byte genericRecursionDepth;
            public byte rank;
            public byte minimumAlignment;
            public byte naturalAligment;
            public byte packingSize;
            public Bitfield0 _bitfield0;
            public Bitfield1 _bitfield1;
            internal enum Bitfield0 : byte
            {
                BIT_initialized_and_no_error = 0,
                initialized_and_no_error = (1 << BIT_initialized_and_no_error),
                BIT_valuetype = 1,
                valuetype = (1 << BIT_valuetype),
                BIT_initialized = 2,
                initialized = (1 << BIT_initialized),
                BIT_enumtype = 3,
                enumtype = (1 << BIT_enumtype),
                BIT_is_generic = 4,
                is_generic = (1 << BIT_is_generic),
                BIT_has_references = 5,
                has_references = (1 << BIT_has_references),
                BIT_init_pending = 6,
                init_pending = (1 << BIT_init_pending),
                BIT_size_init_pending = 7,
                size_init_pending = (1 << BIT_size_init_pending),
            }

            internal enum Bitfield1 : byte
            {
                BIT_size_inited = 0,
                size_inited = (1 << BIT_size_inited),
                BIT_has_finalize = 1,
                has_finalize = (1 << BIT_has_finalize),
                BIT_has_cctor = 2,
                has_cctor = (1 << BIT_has_cctor),
                BIT_is_blittable = 3,
                is_blittable = (1 << BIT_is_blittable),
                BIT_is_import_or_windows_runtime = 4,
                is_import_or_windows_runtime = (1 << BIT_is_import_or_windows_runtime),
                BIT_is_vtable_initialized = 5,
                is_vtable_initialized = (1 << BIT_is_vtable_initialized),
                BIT_has_initialization_error = 6,
                has_initialization_error = (1 << BIT_has_initialization_error),
            }

        }

        internal class NativeStructWrapper : INativeClassStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            private static int _bitfield0offset = Marshal.OffsetOf<Il2CppClass_27_1>(nameof(Il2CppClass_27_1._bitfield0)).ToInt32();
            private static int _bitfield1offset = Marshal.OffsetOf<Il2CppClass_27_1>(nameof(Il2CppClass_27_1._bitfield1)).ToInt32();
            public IntPtr Pointer { get; }
            private Il2CppClass_27_1* _ => (Il2CppClass_27_1*)Pointer;
            public IntPtr VTable => IntPtr.Add(Pointer, sizeof(Il2CppClass_27_1));
            public Il2CppClass* ClassPointer => (Il2CppClass*)Pointer;
            public INativeTypeStruct ByValArg => UnityVersionHandler.Wrap((Il2CppTypeStruct*)&_->byval_arg);
            public INativeTypeStruct ThisArg => UnityVersionHandler.Wrap((Il2CppTypeStruct*)&_->this_arg);
            public ref uint InstanceSize => ref _->instance_size;
            public ref ushort VtableCount => ref _->vtable_count;
            public ref ushort InterfaceCount => ref _->interfaces_count;
            public ref ushort InterfaceOffsetsCount => ref _->interface_offsets_count;
            public ref byte TypeHierarchyDepth => ref _->typeHierarchyDepth;
            public ref int NativeSize => ref _->native_size;
            public ref uint ActualSize => ref _->actualSize;
            public ref ushort MethodCount => ref _->method_count;
            public ref ushort FieldCount => ref _->field_count;
            public ref Il2CppClassAttributes Flags => ref *(Il2CppClassAttributes*)&_->flags;
            public ref IntPtr Name => ref *(IntPtr*)&_->name;
            public ref IntPtr Namespace => ref *(IntPtr*)&_->namespaze;
            public ref Il2CppImage* Image => ref _->image;
            public ref Il2CppClass* Parent => ref _->parent;
            public ref Il2CppClass* ElementClass => ref _->element_class;
            public ref Il2CppClass* CastClass => ref _->castClass;
            public ref Il2CppClass* DeclaringType => ref _->declaringType;
            public ref Il2CppClass* Class => ref _->klass;
            public ref Il2CppFieldInfo* Fields => ref _->fields;
            public ref Il2CppMethodInfo** Methods => ref _->methods;
            public ref Il2CppClass** ImplementedInterfaces => ref _->implementedInterfaces;
            public ref Il2CppRuntimeInterfaceOffsetPair* InterfaceOffsets => ref _->interfaceOffsets;
            public ref Il2CppClass** TypeHierarchy => ref _->typeHierarchy;
            public bool ValueType
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_valuetype);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_valuetype, value);
            }
            public bool Initialized
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_initialized);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_initialized, value);
            }
            public bool EnumType
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_enumtype);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_enumtype, value);
            }
            public bool IsGeneric
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_is_generic);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_is_generic, value);
            }
            public bool HasReferences
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_has_references);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_has_references, value);
            }
            public bool SizeInited
            {
                get => this.CheckBit(_bitfield1offset, (int)Il2CppClass_27_1.Bitfield1.BIT_size_inited);
                set => this.SetBit(_bitfield1offset, (int)Il2CppClass_27_1.Bitfield1.BIT_size_inited, value);
            }
            public bool HasFinalize
            {
                get => this.CheckBit(_bitfield1offset, (int)Il2CppClass_27_1.Bitfield1.BIT_has_finalize);
                set => this.SetBit(_bitfield1offset, (int)Il2CppClass_27_1.Bitfield1.BIT_has_finalize, value);
            }
            public bool IsVtableInitialized
            {
                get => this.CheckBit(_bitfield1offset, (int)Il2CppClass_27_1.Bitfield1.BIT_is_vtable_initialized);
                set => this.SetBit(_bitfield1offset, (int)Il2CppClass_27_1.Bitfield1.BIT_is_vtable_initialized, value);
            }
            public bool InitializedAndNoError
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_initialized_and_no_error);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_27_1.Bitfield0.BIT_initialized_and_no_error, value);
            }
        }

    }

}
