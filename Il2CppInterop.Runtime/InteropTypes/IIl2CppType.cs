using System;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public interface IIl2CppType
{
    static abstract int Size { get; }
    IntPtr ObjectClass { get; }
}
public interface IIl2CppType<TSelf> : IIl2CppType where TSelf : notnull, IIl2CppType<TSelf>
{
    static abstract void WriteToSpan(TSelf? value, Span<byte> span);
    static abstract TSelf? ReadFromSpan(ReadOnlySpan<byte> span);
}
internal interface IIl2CppByReference
{
    int ReferenceSize { get; }
    IntPtr ReferenceObjectClass { get; }
    void WriteReferenceToSpan(Span<byte> span);
    void ReadReferenceFromSpan(ReadOnlySpan<byte> span);
}
internal interface IObject2 : IIl2CppType<IObject2>
{
    static int IIl2CppType.Size => IntPtr.Size;
    static void IIl2CppType<IObject2>.WriteToSpan(IObject2? value, Span<byte> span)
    {
        Il2CppTypeHelper.WritePointer(value.Box(), span);
    }
    static IObject2? IIl2CppType<IObject2>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        IntPtr pointer = Il2CppTypeHelper.ReadPointer(span);
        return (IObject2?)Il2CppObjectPool.Get(pointer);
    }
}
internal struct ValueTuple2<T1, T2> : IIl2CppType<ValueTuple2<T1, T2>> where T1 : IIl2CppType<T1> where T2 : IIl2CppType<T2>
{
    public T1? Item1;
    public T2? Item2;

    static int IIl2CppType.Size => Il2CppInternals_ValueTuple2<T1, T2>.Size;

    readonly nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<ValueTuple2<T1, T2>>.NativeClassPtr;

    static ValueTuple2<T1, T2> IIl2CppType<ValueTuple2<T1, T2>>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        if (Il2CppTypeHelper.IsBlittable<ValueTuple2<T1, T2>>())
        {
            return MemoryMarshal.Read<ValueTuple2<T1, T2>>(span);
        }
        else
        {
            ValueTuple2<T1, T2> result = default;
            result.Item1 = Il2CppTypeHelper.ReadFromSpanAtOffset<T1>(span, Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_0);
            result.Item2 = Il2CppTypeHelper.ReadFromSpanAtOffset<T2>(span, Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_1);
            return result;
        }
    }

    static void IIl2CppType<ValueTuple2<T1, T2>>.WriteToSpan(ValueTuple2<T1, T2> value, Span<byte> span)
    {
        if (Il2CppTypeHelper.IsBlittable<ValueTuple2<T1, T2>>())
        {
            MemoryMarshal.Write(span, in value);
        }
        else
        {
            Il2CppTypeHelper.WriteToSpanAtOffset(value.Item1, span, Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_0);
            Il2CppTypeHelper.WriteToSpanAtOffset(value.Item2, span, Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_1);
        }
    }
}
internal class Class : IIl2CppType<Class>, IIl2CppObjectBase
{
    public IntPtr Pointer;

    static int IIl2CppType.Size => IntPtr.Size;

    nint IIl2CppObjectBase.Pointer => throw new NotImplementedException();
    bool IIl2CppObjectBase.WasCollected => throw new NotImplementedException();

    static Class? IIl2CppType<Class>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        return Il2CppTypeHelper.ReadReference<Class>(span);
    }

    static void IIl2CppType<Class>.WriteToSpan(Class? value, Span<byte> span)
    {
        Il2CppTypeHelper.WriteClass(value, span);
    }
}
internal struct Int48 : IIl2CppType<Int48>
{
    private uint _low;
    private ushort _high;

    static int IIl2CppType.Size => 6;

    readonly nint IIl2CppType.ObjectClass => Il2CppClassPointerStore<Int48>.NativeClassPtr;

    static Int48 IIl2CppType<Int48>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        return MemoryMarshal.Read<Int48>(span);
    }
    static void IIl2CppType<Int48>.WriteToSpan(Int48 value, Span<byte> span)
    {
        MemoryMarshal.Write(span, in value);
    }
}
internal static class Il2CppInternals_ValueTuple2<T1, T2>
{
    internal static readonly int Size;

    internal static readonly IntPtr FieldInfoPtr_Instance_0;

    internal static readonly int FieldOffset_Instance_0;

    internal static readonly IntPtr FieldInfoPtr_Instance_1;

    internal static readonly int FieldOffset_Instance_1;
}
