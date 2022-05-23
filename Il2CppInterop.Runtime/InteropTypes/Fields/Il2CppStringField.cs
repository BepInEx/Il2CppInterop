using System;

namespace Il2CppInterop.Runtime.InteropTypes.Fields;

public unsafe class Il2CppStringField
{
    private readonly IntPtr _fieldPtr;

    private readonly Il2CppObjectBase _obj;

    internal Il2CppStringField(Il2CppObjectBase obj, string fieldName)
    {
        _obj = obj;
        _fieldPtr = IL2CPP.GetIl2CppField(obj.ObjectClass, fieldName);
    }

    public string Value
    {
        get => Get();
        set => Set(value);
    }

    public string? Get()
    {
        var ptr = *GetPointerToData();
        if (ptr == IntPtr.Zero) return null;

        return IL2CPP.Il2CppStringToManaged(ptr);
    }

    public void Set(string value)
    {
        *GetPointerToData() = IL2CPP.ManagedStringToIl2Cpp(value);
    }

    public static implicit operator string(Il2CppStringField _this)
    {
        return _this.Get();
    }

    public static implicit operator Il2CppStringField(string _)
    {
        throw null;
    }

    public override string ToString()
    {
        return Get();
    }

    private IntPtr* GetPointerToData()
    {
        return (IntPtr*)(IL2CPP.Il2CppObjectBaseToPtrNotNull(_obj) + (int)IL2CPP.il2cpp_field_get_offset(_fieldPtr));
    }
}
