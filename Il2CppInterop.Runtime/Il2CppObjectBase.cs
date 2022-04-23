using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime
{
    public class Il2CppObjectBase
    {
        public IntPtr ObjectClass => IL2CPP.il2cpp_object_get_class(Pointer);
        public IntPtr Pointer
        {
            get
            {
                var handleTarget = IL2CPP.il2cpp_gchandle_get_target(myGcHandle);
                if (handleTarget == IntPtr.Zero) throw new ObjectCollectedException("Object was garbage collected in IL2CPP domain");
                return handleTarget;
            }
        }

        public bool WasCollected
        {
            get
            {
                var handleTarget = IL2CPP.il2cpp_gchandle_get_target(myGcHandle);
                if (handleTarget == IntPtr.Zero) return true;
                return false;
            }
        }

        private uint myGcHandle;
        internal bool isWrapped;

        internal void CreateGCHandle(IntPtr objHdl)
        {
            if (objHdl == IntPtr.Zero)
                throw new NullReferenceException();

            // This object already wraps an Il2Cpp object, skip the pointer and let it be GC'd
            if (isWrapped)
                return;

            myGcHandle = RuntimeSpecificsStore.ShouldUseWeakRefs(IL2CPP.il2cpp_object_get_class(objHdl))
                ? IL2CPP.il2cpp_gchandle_new_weakref(objHdl, false)
                : IL2CPP.il2cpp_gchandle_new(objHdl, false);
        }

        public Il2CppObjectBase(IntPtr pointer)
        {
            CreateGCHandle(pointer);
        }

        public T Cast<T>() where T : Il2CppObjectBase
        {
            return TryCast<T>() ?? throw new InvalidCastException($"Can't cast object of type {Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_object_get_class(Pointer)))} to type {typeof(T)}");
        }

        public T Unbox<T>() where T : unmanaged
        {
            var nestedTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
            if (nestedTypeClassPointer == IntPtr.Zero)
                throw new ArgumentException($"{typeof(T)} is not an Il2Cpp reference type");

            var ownClass = IL2CPP.il2cpp_object_get_class(Pointer);
            if (!IL2CPP.il2cpp_class_is_assignable_from(nestedTypeClassPointer, ownClass))
                throw new InvalidCastException($"Can't cast object of type {Marshal.PtrToStringAnsi(IL2CPP.il2cpp_class_get_name(IL2CPP.il2cpp_object_get_class(Pointer)))} to type {typeof(T)}");

            return Marshal.PtrToStructure<T>(IL2CPP.il2cpp_object_unbox(Pointer));
        }

        public T? TryCast<T>() where T : Il2CppObjectBase
        {
            var nestedTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
            if (nestedTypeClassPointer == IntPtr.Zero)
                throw new ArgumentException($"{typeof(T)} is not an Il2Cpp reference type");

            var ownClass = IL2CPP.il2cpp_object_get_class(Pointer);
            if (!IL2CPP.il2cpp_class_is_assignable_from(nestedTypeClassPointer, ownClass))
                return null;

            if (RuntimeSpecificsStore.IsInjected(ownClass))
            {
                var monoObject = ClassInjectorBase.GetMonoObjectFromIl2CppPointer(Pointer) as T;
                if (monoObject != null) return monoObject;
            }

            var type = Il2CppClassPointerStore<T>.CreatedTypeRedirect ?? typeof(T);
            // Base case: Il2Cpp constructor => call it directly
            if (type.GetConstructor(new[] { typeof(IntPtr) }) != null)
                return (T)Activator.CreateInstance(type, Pointer);

            // Special case: We have a parameterless constructor
            // However, it could be be user-made or implicit
            // In that case we set the GCHandle and then call the ctor and let GC destroy any objects created by DerivedConstructorPointer
            var obj = (T)FormatterServices.GetUninitializedObject(type);
            obj.CreateGCHandle(Pointer);
            obj.isWrapped = true;
            var ctor = type.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null,
                Type.EmptyTypes, Array.Empty<ParameterModifier>());
            if (ctor != null)
                ctor.Invoke(obj, null);
            return obj;
        }

        ~Il2CppObjectBase()
        {
            IL2CPP.il2cpp_gchandle_free(myGcHandle);
        }
    }
}