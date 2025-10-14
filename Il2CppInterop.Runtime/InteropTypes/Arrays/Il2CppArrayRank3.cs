using System.Diagnostics.CodeAnalysis;
using Il2CppInterop.Common;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public sealed class Il2CppArrayRank3<T> : Il2CppMultiArrayBase<T> where T : IIl2CppType<T>
{
    static Il2CppArrayRank3() => SetClassPointer<Il2CppArrayRank3<T>, T>(3);

    public Il2CppArrayRank3(ObjectPointer pointer) : base(pointer)
    {
    }

    public Il2CppArrayRank3(int length0, int length1, int length2) : base([length0, length1, length2], Il2CppClassPointerStore<Il2CppArrayRank3<T>>.NativeClassPtr)
    {
    }

    public Il2CppArrayRank3(T[,,] values) : this(values.GetLength(0), values.GetLength(1), values.GetLength(2))
    {
        var length_0 = values.GetLength(0);
        var length_1 = values.GetLength(1);
        var length_2 = values.GetLength(2);

        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            for (var i_1 = 0; i_1 < length_1; i_1++)
            {
                for (var i_2 = 0; i_2 < length_2; i_2++)
                {
                    this[i_0, i_1, i_2] = values[i_0, i_1, i_2];
                }
            }
        }
    }

    public T this[int index0, int index1]
    {
        get => this[[index0, index1]];
        set => this[[index0, index1]] = value;
    }
    public ByReference<T> GetElementAddress(int index0, int index1, int index2) => GetElementAddress([index0, index1, index2]);

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator Il2CppArrayRank3<T>?(T[,,]? array) => array is null ? null : new(array);

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator T[,,]?(Il2CppArrayRank3<T>? array)
    {
        if (array is null)
            return null;

        var length_0 = array.GetLength(0);
        var length_1 = array.GetLength(1);
        var length_2 = array.GetLength(2);
        var result = new T[length_0, length_1, length_2];
        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            for (var i_1 = 0; i_1 < length_1; i_1++)
            {
                for (var i_2 = 0; i_2 < length_2; i_2++)
                {
                    result[i_0, i_1, i_2] = array[i_0, i_1, i_2];
                }
            }
        }
        return result;
    }
}
