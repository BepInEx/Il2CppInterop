using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime.VersionSpecific.Type;
namespace Il2CppInterop.Runtime.Runtime.VersionSpecific.Class
{
    // Unity 6000.5 (IL2CPP metadata v107) restructured Il2CppClass:
    //  * methods/implementedInterfaces/interfaceOffsets/static_fields/rgctx_data/typeHierarchy moved ahead of
    //    the lazily created events/properties/nestedTypes pointers,
    //  * genericContainerHandle moved in after cctor_thread,
    //  * stack_slot_size is still present (unlike the layout table Il2CppDumper emits for this version).
    // Offsets measured at runtime against a Unity 6000.5.3f1 GameAssembly.dll by matching the il2cpp API
    // (il2cpp_class_get_methods/get_interfaces) against raw class memory for System.Object, System.String,
    // IEnumerator and UnityEngine.MonoBehaviour (methods@136, implementedInterfaces@144, interfaceOffsets@152,
    // typeHierarchy@176, unity_user_data@208, instance_size@248, flags@280, bitfields@309, vtable@312).
    [ApplicableToUnityVersionsSince("6000.5.0")]
    public unsafe class NativeClassStructHandler_6000_5 : INativeClassStructHandler
    {
        public int Size() => sizeof(Il2CppClass_6000_5);
        public INativeClassStruct CreateNewStruct(int vTableSlots)
        {
            IntPtr ptr = Marshal.AllocHGlobal(Size() + sizeof(VirtualInvokeData) * vTableSlots);
            Il2CppClass_6000_5* _ = (Il2CppClass_6000_5*)ptr;
            *_ = default;
            return new NativeStructWrapper(ptr);
        }
        public INativeClassStruct Wrap(Il2CppClass* ptr)
        {
            if (ptr == null) return null;
            return new NativeStructWrapper((IntPtr)ptr);
        }
        internal unsafe struct Il2CppClass_6000_5
        {
            public Il2CppImage* image;                                  // 0
            public void* gc_desc;                                       // 8
            public byte* name;                                          // 16
            public byte* namespaze;                                     // 24
            public NativeTypeStructHandler_27_0.Il2CppType_27_0 byval_arg; // 32
            public NativeTypeStructHandler_27_0.Il2CppType_27_0 this_arg;  // 48
            public Il2CppClass* element_class;                          // 64
            public Il2CppClass* castClass;                              // 72
            public Il2CppClass* declaringType;                          // 80
            public Il2CppClass* parent;                                 // 88
            public void* generic_class;                                 // 96
            public Il2CppMetadataTypeHandle typeMetadataHandle;         // 104
            public void* interopData;                                   // 112
            public Il2CppClass* klass;                                  // 120
            public Il2CppFieldInfo* fields;                             // 128
            public Il2CppMethodInfo** methods;                          // 136
            public Il2CppClass** implementedInterfaces;                 // 144
            public Il2CppRuntimeInterfaceOffsetPair* interfaceOffsets;  // 152
            public void* static_fields;                                 // 160
            public void* rgctx_data;                                    // 168
            public Il2CppClass** typeHierarchy;                         // 176
            public Il2CppEventInfo* events;                             // 184 (lazily created)
            public Il2CppPropertyInfo* properties;                      // 192 (lazily created)
            public Il2CppClass** nestedTypes;                           // 200 (lazily created)
            public void* unity_user_data;                               // 208
            public Il2CppGCHandle initializationExceptionGCHandle;      // 216
            public uint cctor_started;                                  // 224
            public uint cctor_finished_or_no_cctor;                     // 228
            public IntPtr cctor_thread;                                 // 232
            public void* genericContainerHandle;                        // 240
            public uint instance_size;                                  // 248
            public uint stack_slot_size;                                // 252
            public uint actualSize;                                     // 256
            public uint element_size;                                   // 260
            public int native_size;                                     // 264
            public uint static_fields_size;                             // 268
            public uint thread_static_fields_size;                      // 272
            public int thread_static_fields_offset;                     // 276
            public uint flags;                                          // 280
            public uint token;                                          // 284
            public ushort method_count;                                 // 288
            public ushort property_count;                               // 290
            public ushort field_count;                                  // 292
            public ushort event_count;                                  // 294
            public ushort nested_type_count;                            // 296
            public ushort vtable_count;                                 // 298
            public ushort interfaces_count;                             // 300
            public ushort interface_offsets_count;                      // 302
            public byte typeHierarchyDepth;                             // 304
            public byte genericRecursionDepth;                          // 305
            public byte rank;                                           // 306
            public byte minimumAlignment;                               // 307
            public byte packingSize;                                    // 308
            public byte _bitfield0;                                     // 309 (initialized group)
            public byte _bitfield1;                                     // 310
            public byte _bitfield2;                                     // 311 (is_byref_like, has_inline_array)
            internal enum Bitfield0 : byte
            {
                BIT_initialized_and_no_error = 0,
                initialized_and_no_error = (1 << BIT_initialized_and_no_error),
                BIT_initialized = 1,
                initialized = (1 << BIT_initialized),
                BIT_enumtype = 2,
                enumtype = (1 << BIT_enumtype),
                BIT_nullabletype = 3,
                nullabletype = (1 << BIT_nullabletype),
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
                BIT_is_byref_like = 6,
                is_byref_like = (1 << BIT_is_byref_like),
            }

        }

        internal class NativeStructWrapper : INativeClassStruct
        {
            public NativeStructWrapper(IntPtr ptr) => Pointer = ptr;
            private static int _bitfield0offset = Marshal.OffsetOf<Il2CppClass_6000_5>(nameof(Il2CppClass_6000_5._bitfield0)).ToInt32();
            private static int _bitfield1offset = Marshal.OffsetOf<Il2CppClass_6000_5>(nameof(Il2CppClass_6000_5._bitfield1)).ToInt32();
            public IntPtr Pointer { get; }
            private Il2CppClass_6000_5* _ => (Il2CppClass_6000_5*)Pointer;
            public IntPtr VTable => IntPtr.Add(Pointer, sizeof(Il2CppClass_6000_5));
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
                get => ByValArg.ValueType && ThisArg.ValueType;
                set { }
            }
            public bool Initialized
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_initialized);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_initialized, value);
            }
            public bool EnumType
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_enumtype);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_enumtype, value);
            }
            public bool IsGeneric
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_is_generic);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_is_generic, value);
            }
            public bool HasReferences
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_has_references);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_has_references, value);
            }
            public bool SizeInited
            {
                get => this.CheckBit(_bitfield1offset, (int)Il2CppClass_6000_5.Bitfield1.BIT_size_inited);
                set => this.SetBit(_bitfield1offset, (int)Il2CppClass_6000_5.Bitfield1.BIT_size_inited, value);
            }
            public bool HasFinalize
            {
                get => this.CheckBit(_bitfield1offset, (int)Il2CppClass_6000_5.Bitfield1.BIT_has_finalize);
                set => this.SetBit(_bitfield1offset, (int)Il2CppClass_6000_5.Bitfield1.BIT_has_finalize, value);
            }
            public bool IsVtableInitialized
            {
                get => this.CheckBit(_bitfield1offset, (int)Il2CppClass_6000_5.Bitfield1.BIT_is_vtable_initialized);
                set => this.SetBit(_bitfield1offset, (int)Il2CppClass_6000_5.Bitfield1.BIT_is_vtable_initialized, value);
            }
            public bool InitializedAndNoError
            {
                get => this.CheckBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_initialized_and_no_error);
                set => this.SetBit(_bitfield0offset, (int)Il2CppClass_6000_5.Bitfield0.BIT_initialized_and_no_error, value);
            }
        }

    }

}
