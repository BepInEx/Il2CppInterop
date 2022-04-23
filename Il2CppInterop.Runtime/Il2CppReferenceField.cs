using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime
{
    public unsafe class Il2CppReferenceField<TRefObj> where TRefObj : Il2CppObjectBase
    {
        private static bool? isInjectedType = null;

        internal Il2CppReferenceField(Il2CppObjectBase obj, string fieldName)
        {
            _obj = obj;
            _fieldPtr = IL2CPP.GetIl2CppField(obj.ObjectClass, fieldName);
        }

        public TRefObj? Get()
        {
            IntPtr ptr = *GetPointerToData();
            if (ptr == IntPtr.Zero) return null;
            if (isInjectedType == null) isInjectedType = RuntimeSpecificsStore.IsInjected(Il2CppClassPointerStore<TRefObj>.NativeClassPtr);

            if (isInjectedType.Value && ClassInjectorBase.GetMonoObjectFromIl2CppPointer(ptr) is TRefObj monoObject) return monoObject;
            return (TRefObj)Activator.CreateInstance(typeof(TRefObj), ptr);
        }

        public void Set(TRefObj value) => *GetPointerToData() = value.Pointer;

        public static implicit operator TRefObj(Il2CppReferenceField<TRefObj> _this) => _this.Get();
        public static implicit operator Il2CppReferenceField<TRefObj>(TRefObj _) => throw null;

        private IntPtr* GetPointerToData() => (IntPtr*)(IL2CPP.Il2CppObjectBaseToPtrNotNull(_obj) + (int)IL2CPP.il2cpp_field_get_offset(_fieldPtr));

        private readonly Il2CppObjectBase _obj;
        private readonly IntPtr _fieldPtr;
    }
}
