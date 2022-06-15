using System;

namespace Il2CppInterop.Runtime.InteropTypes.Fields;

public unsafe class Il2CppValueField<T> where T : unmanaged
{
    private readonly IntPtr _fieldPtr;

    private readonly Il2CppObjectBase _obj;

    internal Il2CppValueField(Il2CppObjectBase obj, string fieldName)
    {
        _obj = obj;
        _fieldPtr = IL2CPP.GetIl2CppField(obj.ObjectClass, fieldName);
    }

    public T Value
    {
        get => Get();
        set => Set(value);
    }

    public T Get()
    {
        return *GetPointerToData();
    }

    public void Set(T value)
    {
        *GetPointerToData() = value;
    }

    public static implicit operator T(Il2CppValueField<T> _this)
    {
        return _this.Get();
    }

    public static implicit operator Il2CppValueField<T>(T _)
    {
        throw null;
    }

    private T* GetPointerToData()
    {
        return (T*)(IL2CPP.Il2CppObjectBaseToPtrNotNull(_obj) + (int)IL2CPP.il2cpp_field_get_offset(_fieldPtr));
    }
}
