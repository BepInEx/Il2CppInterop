using System;
using System.Diagnostics.CodeAnalysis;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public sealed class Il2CppStringArray : Il2CppArrayBase<string>
{
    static Il2CppStringArray()
    {
        StaticCtorBody(typeof(Il2CppStringArray));
    }

    public Il2CppStringArray(IntPtr pointer) : base(pointer)
    {
    }

    public Il2CppStringArray(long size) : base(AllocateArray(size))
    {
    }

    public Il2CppStringArray(string?[] arr) : base(AllocateArray(arr.Length))
    {
        for (var i = 0; i < arr.Length; i++)
            this[i] = arr[i];
    }
#nullable disable
    public override unsafe string this[int index]
    {
        get => IL2CPP.Il2CppStringToManaged(*GetElementPointer(index));
        set => *GetElementPointer(index) = IL2CPP.ManagedStringToIl2Cpp(value);
    }
#nullable enable
    private unsafe IntPtr* GetElementPointer(int index)
    {
        ThrowIfIndexOutOfRange(index);
        return (IntPtr*)IntPtr.Add(ArrayStartPointer, index * IntPtr.Size).ToPointer();
    }

    [return: NotNullIfNotNull(nameof(arr))]
    public static implicit operator Il2CppStringArray?(string?[]? arr)
    {
        if (arr == null) return null;

        return new Il2CppStringArray(arr);
    }

    private static IntPtr AllocateArray(long size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Array size must not be negative");

        var elementTypeClassPointer = Il2CppClassPointerStore<string>.NativeClassPtr;
        if (elementTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException("String class pointer is missing, something is very wrong");
        return IL2CPP.il2cpp_array_new(elementTypeClassPointer, (ulong)size);
    }
}
