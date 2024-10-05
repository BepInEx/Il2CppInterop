using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public class Il2CppReferenceArray<T> : Il2CppArrayBase<T> where T : Il2CppObjectBase?
{
    private static readonly int ourElementTypeSize;
    private static readonly bool ourElementIsValueType;

    static Il2CppReferenceArray()
    {
        ourElementTypeSize = IntPtr.Size;
        var nativeClassPtr = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (nativeClassPtr == IntPtr.Zero) return;
        uint align = 0;
        if (IL2CPP.il2cpp_class_is_valuetype(nativeClassPtr))
        {
            ourElementIsValueType = true;
            ourElementTypeSize = IL2CPP.il2cpp_class_value_size(nativeClassPtr, ref align);
        }

        StaticCtorBody(typeof(Il2CppReferenceArray<T>));
    }

    public Il2CppReferenceArray(IntPtr nativeObject) : base(nativeObject)
    {
    }

    public Il2CppReferenceArray(long size) : base(AllocateArray(size))
    {
    }

    public Il2CppReferenceArray(T[] arr) : base(AllocateArray(arr.Length))
    {
        for (var i = 0; i < arr.Length; i++)
            this[i] = arr[i];
    }

    public override T this[int index]
    {
        get => WrapElement(GetElementPointer(index))!;
        set => StoreValue(GetElementPointer(index), value?.Pointer ?? IntPtr.Zero);
    }

    private IntPtr GetElementPointer(int index)
    {
        ThrowIfIndexOutOfRange(index);
        return IntPtr.Add(ArrayStartPointer, index * ourElementTypeSize);
    }

    [return: NotNullIfNotNull(nameof(arr))]
    public static implicit operator Il2CppReferenceArray<T>?(T[]? arr)
    {
        if (arr == null) return null;

        return new Il2CppReferenceArray<T>(arr);
    }

    private static unsafe void StoreValue(IntPtr targetPointer, IntPtr valuePointer)
    {
        if (ourElementIsValueType)
        {
            if (valuePointer == IntPtr.Zero)
                throw new NullReferenceException();

            var valueRawPointer = (byte*)IL2CPP.il2cpp_object_unbox(valuePointer);
            var targetRawPointer = (byte*)targetPointer;

            Unsafe.CopyBlock(targetRawPointer, valueRawPointer, (uint)ourElementTypeSize);
        }
        else
        {
            *(IntPtr*)targetPointer = valuePointer;
        }
    }

    private static unsafe T? WrapElement(IntPtr memberPointer)
    {
        if (ourElementIsValueType)
            memberPointer = IL2CPP.il2cpp_value_box(Il2CppClassPointerStore<T>.NativeClassPtr, memberPointer);
        else
            memberPointer = *(IntPtr*)memberPointer;

        if (memberPointer == IntPtr.Zero)
            return default;

        return Il2CppObjectPool.Get<T>(memberPointer);
    }

    private static IntPtr AllocateArray(long size)
    {
        if (size < 0)
            throw new ArgumentOutOfRangeException(nameof(size), "Array size must not be negative");

        var elementTypeClassPointer = Il2CppClassPointerStore<T>.NativeClassPtr;
        if (elementTypeClassPointer == IntPtr.Zero)
            throw new ArgumentException(
                $"{nameof(Il2CppReferenceArray<T>)} requires an Il2Cpp reference type, which {typeof(T)} isn't");
        return IL2CPP.il2cpp_array_new(elementTypeClassPointer, (ulong)size);
    }
}
