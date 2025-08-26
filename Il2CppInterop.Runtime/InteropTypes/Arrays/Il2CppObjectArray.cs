using System;
using System.Diagnostics.CodeAnalysis;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public sealed class Il2CppObjectArray : Il2CppArrayBase<object>
{
    static Il2CppObjectArray()
    {
        StaticCtorBody(typeof(Il2CppObjectArray));
    }

    public Il2CppObjectArray(IntPtr pointer) : base(pointer)
    {
    }
    public Il2CppObjectArray(ObjectPointer pointer) : base(pointer)
    {
    }

    public Il2CppObjectArray(long size) : base(AllocateArray(size))
    {
    }

    public Il2CppObjectArray(object?[] arr) : base(AllocateArray(arr.Length))
    {
        for (var i = 0; i < arr.Length; i++)
            this[i] = arr[i];
    }
#nullable disable
    public override unsafe object this[int index]
    {
        get => throw new NotImplementedException();
        set => throw new NotImplementedException();
    }
#nullable enable
    private unsafe IntPtr* GetElementPointer(int index)
    {
        ThrowIfIndexOutOfRange(index);
        return (IntPtr*)IntPtr.Add(ArrayStartPointer, index * IntPtr.Size).ToPointer();
    }

    [return: NotNullIfNotNull(nameof(arr))]
    public static implicit operator Il2CppObjectArray?(object?[]? arr)
    {
        if (arr == null)
            return null;

        return new Il2CppObjectArray(arr);
    }

    private static IntPtr AllocateArray(long size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Array size must not be negative");

        var elementTypeClassPointer = Il2CppClassPointerStore<object>.NativeClassPtr;
        if (elementTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException("String class pointer is missing, something is very wrong");
        return IL2CPP.il2cpp_array_new(elementTypeClassPointer, (ulong)size);
    }
}
