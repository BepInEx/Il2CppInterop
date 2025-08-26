using System;
using System.Diagnostics.CodeAnalysis;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public sealed class Il2CppNonBlittableArray<T> : Il2CppArrayBase<T> where T : IIl2CppType<T>
{
    static Il2CppNonBlittableArray()
    {
        StaticCtorBody(typeof(Il2CppNonBlittableArray<T>));
    }

    public Il2CppNonBlittableArray(IntPtr nativeObject) : base(nativeObject)
    {
    }
    public Il2CppNonBlittableArray(ObjectPointer nativeObject) : base(nativeObject)
    {
    }

    public Il2CppNonBlittableArray(long size) : base(AllocateArray(size))
    {
    }

    public Il2CppNonBlittableArray(T[] arr) : base(AllocateArray(arr.Length))
    {
        for (var i = 0; i < arr.Length; i++)
            this[i] = arr[i];
    }

    public override T this[int index]
    {
        get
        {
            ThrowIfIndexOutOfRange(index);
            return T.ReadFromSpan(AsSpan().Slice(index * T.Size, T.Size));
        }
        set
        {
            ThrowIfIndexOutOfRange(index);
            T.WriteToSpan(value, AsSpan().Slice(index * T.Size, T.Size));
        }
    }

    private unsafe Span<byte> AsSpan()
    {
        return new Span<byte>(ArrayStartPointer.ToPointer(), Length * T.Size);
    }

    private protected override unsafe Span<byte> GetUnsafeSpanForElement(int index)
    {
        ThrowIfIndexOutOfRange(index);
        return new Span<byte>((byte*)ArrayStartPointer.ToPointer() + index * T.Size, T.Size);
    }

    [return: NotNullIfNotNull(nameof(arr))]
    public static implicit operator Il2CppNonBlittableArray<T>?(T[]? arr)
    {
        if (arr == null)
            return null;

        return new Il2CppNonBlittableArray<T>(arr);
    }

    private static IntPtr AllocateArray(long size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Array size must not be negative");

        var elementTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (elementTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException(
                $"{nameof(Il2CppNonBlittableArray<T>)} requires an Il2Cpp type, which {typeof(T)} isn't");
        return IL2CPP.il2cpp_array_new(elementTypeClassPointer, (ulong)size);
    }
}
