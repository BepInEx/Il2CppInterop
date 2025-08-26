using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes;

public interface IIl2CppType
{
    static abstract int Size { get; }
}
internal interface IObject2 : IIl2CppValueType
{

}
internal class Object2 : IObject2
{
    int IIl2CppValueType.Size => throw new NotImplementedException();

    nint IIl2CppValueType.ObjectClass => throw new NotImplementedException();

    void IIl2CppValueType.ReadFromSpan(ReadOnlySpan<byte> span) => throw new NotImplementedException();
    void IIl2CppValueType.WriteToSpan(Span<byte> span) => throw new NotImplementedException();
}
public interface IIl2CppType<TSelf> : IIl2CppType where TSelf : notnull, IIl2CppType<TSelf>
{
    static abstract void WriteToSpan(TSelf? value, Span<byte> span);
    static abstract TSelf? ReadFromSpan(ReadOnlySpan<byte> span);
}
public interface IIl2CppValueType
{
    int Size { get; }
    IntPtr ObjectClass { get; }
    void WriteToSpan(Span<byte> span);
    void ReadFromSpan(ReadOnlySpan<byte> span);
}
internal interface IIl2CppByReference
{
    int ReferenceSize { get; }
    IntPtr ReferenceObjectClass { get; }
    void WriteReferenceToSpan(Span<byte> span);
    void ReadReferenceFromSpan(ReadOnlySpan<byte> span);
}
internal struct ValueTuple2<T1, T2> : IIl2CppType<ValueTuple2<T1, T2>> where T1 : IIl2CppType<T1> where T2 : IIl2CppType<T2>
{
    public T1 Item1;
    public T2 Item2;

    static int IIl2CppType.Size => Il2CppInternals_ValueTuple2<T1, T2>.Size;

    static ValueTuple2<T1, T2> IIl2CppType<ValueTuple2<T1, T2>>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        if (Il2CppTypeHelper.IsBlittable<ValueTuple2<T1, T2>>())
        {
            return MemoryMarshal.Read<ValueTuple2<T1, T2>>(span);
        }
        else
        {
            ValueTuple2<T1, T2> result = default;
            result.Item1 = Il2CppTypeHelper.ReadFromSpan<T1>(span.Slice(Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_0, Il2CppTypeHelper.SizeOf<T1>()));
            result.Item2 = Il2CppTypeHelper.ReadFromSpan<T2>(span.Slice(Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_1, Il2CppTypeHelper.SizeOf<T2>()));
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
            Il2CppTypeHelper.WriteToSpan(value.Item1, span.Slice(Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_0, Il2CppTypeHelper.SizeOf<T1>()));
            Il2CppTypeHelper.WriteToSpan(value.Item2, span.Slice(Il2CppInternals_ValueTuple2<T1, T2>.FieldOffset_Instance_1, Il2CppTypeHelper.SizeOf<T2>()));
        }
    }
}
internal class Class : IIl2CppType<Class>, IIl2CppObjectBase
{
    public IntPtr Pointer;

    static int IIl2CppType.Size => IntPtr.Size;

    nint IIl2CppObjectBase.Pointer => throw new NotImplementedException();

    bool IIl2CppObjectBase.WasCollected => throw new NotImplementedException();

    int IIl2CppValueType.Size => throw new NotImplementedException();

    static Class? IIl2CppType<Class>.ReadFromSpan(ReadOnlySpan<byte> span)
    {
        return Il2CppTypeHelper.ReadClass<Class>(span);
    }

    static void IIl2CppType<Class>.WriteToSpan(Class? value, Span<byte> span)
    {
        Il2CppTypeHelper.WriteClass(value, span);
    }

    void IIl2CppValueType.ReadFromSpan(ReadOnlySpan<byte> span) => throw new NotImplementedException();
    void IIl2CppValueType.WriteToSpan(Span<byte> span) => throw new NotImplementedException();
}
internal struct Int48 : IIl2CppType<Int48>
{
    private uint _low;
    private ushort _high;

    static int IIl2CppType.Size => 6;
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
