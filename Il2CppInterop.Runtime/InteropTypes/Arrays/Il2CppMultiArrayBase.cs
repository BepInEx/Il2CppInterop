using System;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public abstract class Il2CppMultiArrayBase<T> : Il2CppObjectBase // Should this be inheriting from Il2CppSystem.Array?
{
    private protected Il2CppMultiArrayBase(ObjectPointer pointer) : base((nint)pointer)
    {
    }

    public int Rank => throw new NotImplementedException();

    public T this[params ReadOnlySpan<int> indices]
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }

    public ref T GetElementAddress(params ReadOnlySpan<int> indices)
    {
        throw new NotImplementedException();
    }
}
