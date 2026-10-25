using Il2CppInterop.Common;

namespace Il2CppSystem;

public abstract class ValueType : Object, IIl2CppType<ValueType>
{
    static int IIl2CppType<ValueType>.Size => throw new System.NotImplementedException();

    static ValueType IIl2CppType<ValueType>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw new System.NotImplementedException();
    static void IIl2CppType<ValueType>.WriteToSpan(ValueType value, System.Span<byte> span) => throw new System.NotImplementedException();
}
