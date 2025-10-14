using System;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Fields;

public unsafe class Il2CppReferenceField<TRefObj> where TRefObj : Object
{
    private readonly IntPtr _fieldPtr;
    private readonly Object _obj;

    internal Il2CppReferenceField(Object obj, string fieldName)
    {
        _obj = obj;
        _fieldPtr = IL2CPP.GetIl2CppField(((IIl2CppType)obj).ObjectClass, fieldName);
    }

    public TRefObj Value
    {
        get => Get();
        set => Set(value);
    }

    public TRefObj? Get()
    {
        var ptr = *GetPointerToData();
        return ptr == IntPtr.Zero ? null : (TRefObj?)Il2CppObjectPool.Get(ptr);
    }

    public void Set(TRefObj value)
    {
        *GetPointerToData() = value != null ? value.Pointer : IntPtr.Zero;
    }

    public static implicit operator TRefObj(Il2CppReferenceField<TRefObj> _this)
    {
        return _this.Get();
    }

    public static implicit operator Il2CppReferenceField<TRefObj>(TRefObj _)
    {
        throw null;
    }

    private IntPtr* GetPointerToData()
    {
        return (IntPtr*)(IL2CPP.Il2CppObjectToPtrNotNull(_obj) + (int)IL2CPP.il2cpp_field_get_offset(_fieldPtr));
    }
}
