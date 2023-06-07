using System;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Fields;

public unsafe class Il2CppReferenceField<TRefObj> where TRefObj : Il2CppObjectBase
{
    private static bool? isInjectedType;
    private readonly IntPtr _fieldPtr;

    private readonly Il2CppObjectBase _obj;

    internal Il2CppReferenceField(Il2CppObjectBase obj, string fieldName)
    {
        _obj = obj;
        _fieldPtr = IL2CPP.GetIl2CppField(obj.ObjectClass, fieldName);
    }

    public TRefObj Value
    {
        get => Get();
        set => Set(value);
    }

    public TRefObj? Get()
    {
        var ptr = *GetPointerToData();
        if (ptr == IntPtr.Zero) return null;
        if (isInjectedType == null)
            isInjectedType = RuntimeSpecificsStore.IsInjected(Il2CppClassPointerStore<TRefObj>.NativeClassPtr);

        if (isInjectedType.Value && ClassInjectorBase.GetMonoObjectFromIl2CppPointer(ptr) is TRefObj monoObject)
            return monoObject;
        return (TRefObj)Activator.CreateInstance(typeof(TRefObj), ptr);
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
        return (IntPtr*)(IL2CPP.Il2CppObjectBaseToPtrNotNull(_obj) + (int)IL2CPP.il2cpp_field_get_offset(_fieldPtr));
    }
}
