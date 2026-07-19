using System;
using System.Diagnostics.CodeAnalysis;
using Il2CppInterop.Common;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public sealed class Il2CppArrayRank5<T> : Il2CppArrayBase<T>, IIl2CppType<Il2CppArrayRank5<T>>
    where T : IIl2CppType<T>
{
    static Il2CppArrayRank5()
    {
        SetClassPointer<Il2CppArrayRank5<T>, T>(5);
    }

    public Il2CppArrayRank5(ObjectPointer pointer) : base(pointer)
    {
    }

    public Il2CppArrayRank5(int length0, int length1, int length2, int length3, int length4) : base([length0, length1, length2, length3, length4], Il2CppType.GetClassPointer<Il2CppArrayRank5<T>>())
    {
    }

    public Il2CppArrayRank5(T[,,,,] values) : this(values.GetLength(0), values.GetLength(1), values.GetLength(2), values.GetLength(3), values.GetLength(4))
    {
        var length_0 = values.GetLength(0);
        var length_1 = values.GetLength(1);
        var length_2 = values.GetLength(2);
        var length_3 = values.GetLength(3);
        var length_4 = values.GetLength(4);

        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            for (var i_1 = 0; i_1 < length_1; i_1++)
            {
                for (var i_2 = 0; i_2 < length_2; i_2++)
                {
                    for (var i_3 = 0; i_3 < length_3; i_3++)
                    {
                        for (var i_4 = 0; i_4 < length_4; i_4++)
                        {
                            this[i_0, i_1, i_2, i_3, i_4] = values[i_0, i_1, i_2, i_3, i_4];
                        }
                    }
                }
            }
        }
    }

    public T this[int index0, int index1, int index2, int index3, int index4]
    {
        get => this[[index0, index1, index2, index3, index4]];
        set => this[[index0, index1, index2, index3, index4]] = value;
    }

    public ByReference<T> GetElementAddress(int index0, int index1, int index2, int index3, int index4) => GetElementAddress([index0, index1, index2, index3, index4]);

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator Il2CppArrayRank5<T>?(T[,,,,]? array) => array is null ? null : new(array);

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator T[,,,,]?(Il2CppArrayRank5<T>? array)
    {
        if (array is null)
            return null;

        var length_0 = array.GetLength(0);
        var length_1 = array.GetLength(1);
        var length_2 = array.GetLength(2);
        var length_3 = array.GetLength(3);
        var length_4 = array.GetLength(4);
        var result = new T[length_0, length_1, length_2, length_3, length_4];
        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            for (var i_1 = 0; i_1 < length_1; i_1++)
            {
                for (var i_2 = 0; i_2 < length_2; i_2++)
                {
                    for (var i_3 = 0; i_3 < length_3; i_3++)
                    {
                        for (var i_4 = 0; i_4 < length_4; i_4++)
                        {
                            result[i_0, i_1, i_2, i_3, i_4] = array[i_0, i_1, i_2, i_3, i_4];
                        }
                    }
                }
            }
        }
        return result;
    }

    #region IIl2CppType Implementation
    nint IIl2CppType.ObjectClass => Il2CppType.GetClassPointer<Il2CppArrayRank5<T>>();
    static void IIl2CppType<Il2CppArrayRank5<T>>.WriteToSpan(Il2CppArrayRank5<T>? value, Span<byte> span) => Il2CppType.WriteReference(value, span);
    static Il2CppArrayRank5<T>? IIl2CppType<Il2CppArrayRank5<T>>.ReadFromSpan(ReadOnlySpan<byte> span) => Il2CppType.ReadReference<Il2CppArrayRank5<T>>(span);
    static Il2CppArrayRank5<T> IIl2CppType<Il2CppArrayRank5<T>>.UnboxNative(ObjectPointer pointer) => new(pointer);
    #endregion
}
