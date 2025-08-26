using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public interface IIl2CppObjectBase : IIl2CppValueType
{
    IntPtr Pointer { get; }
    IntPtr IIl2CppValueType.ObjectClass => IL2CPP.il2cpp_object_get_class(Pointer);
    bool WasCollected { get; }
}
public interface IIl2CppObjectBase<TSelf> : IIl2CppObjectBase where TSelf : IIl2CppObjectBase<TSelf>
{
    static abstract TSelf Create(IntPtr ptr);
}
