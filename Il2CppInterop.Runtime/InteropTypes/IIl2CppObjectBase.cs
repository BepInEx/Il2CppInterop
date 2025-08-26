using System;

namespace Il2CppInterop.Runtime.InteropTypes;

public interface IIl2CppObjectBase : IIl2CppType
{
    IntPtr Pointer { get; }
    IntPtr IIl2CppType.ObjectClass => IL2CPP.il2cpp_object_get_class(Pointer);
    bool WasCollected { get; }
}
public interface IIl2CppObjectBase<TSelf> : IIl2CppObjectBase, IIl2CppType<TSelf>
    where TSelf : IIl2CppObjectBase<TSelf>
{
    static abstract TSelf Create(IntPtr ptr);
}
