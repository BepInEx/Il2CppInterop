using System;
using Il2CppInterop.Runtime;

namespace Il2CppInterop.Runtime
{
    public unsafe class Il2CppValueField<T> where T : unmanaged
    {
        internal Il2CppValueField(Il2CppObjectBase obj, string fieldName)
        {
            _obj = obj;
            _fieldPtr = IL2CPP.GetIl2CppField(obj.ObjectClass, fieldName);
        }

        public T Get() => *GetPointerToData();
        public void Set(T value) => *GetPointerToData() = value;

        public static implicit operator T(Il2CppValueField<T> _this) => _this.Get();
        public static implicit operator Il2CppValueField<T>(T _) => throw null;

        private T* GetPointerToData() => (T*)(IL2CPP.Il2CppObjectBaseToPtrNotNull(_obj) + (int)IL2CPP.il2cpp_field_get_offset(_fieldPtr));

        private readonly Il2CppObjectBase _obj;
        private readonly IntPtr _fieldPtr;
    }
}
