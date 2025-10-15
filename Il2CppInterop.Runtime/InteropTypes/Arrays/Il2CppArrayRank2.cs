using System.Diagnostics.CodeAnalysis;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Runtime;

namespace Il2CppInterop.Runtime.InteropTypes.Arrays;

public sealed class Il2CppArrayRank2<T> : Il2CppMultiArrayBase<T> where T : IIl2CppType<T>
{
    static Il2CppArrayRank2()
    {
        SetClassPointer<Il2CppArrayRank2<T>, T>(2);
        Il2CppObjectPool.RegisterInitializer(Il2CppClassPointerStore<Il2CppArrayRank2<T>>.NativeClassPtr, static (ptr) => new Il2CppArrayRank2<T>(ptr));
    }

    public Il2CppArrayRank2(ObjectPointer pointer) : base(pointer)
    {
    }

    public Il2CppArrayRank2(int length0, int length1) : base([length0, length1], Il2CppClassPointerStore<Il2CppArrayRank2<T>>.NativeClassPtr)
    {
    }

    public Il2CppArrayRank2(T[,] values) : this(values.GetLength(0), values.GetLength(1))
    {
        var length_0 = values.GetLength(0);
        var length_1 = values.GetLength(1);

        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            for (var i_1 = 0; i_1 < length_1; i_1++)
            {
                this[i_0, i_1] = values[i_0, i_1];
            }
        }
    }

    public T this[int index0, int index1]
    {
        get => this[[index0, index1]];
        set => this[[index0, index1]] = value;
    }
    public ByReference<T> GetElementAddress(int index0, int index1) => GetElementAddress([index0, index1]);

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator Il2CppArrayRank2<T>?(T[,]? array) => array is null ? null : new(array);

    [return: NotNullIfNotNull(nameof(array))]
    public static explicit operator T[,]?(Il2CppArrayRank2<T>? array)
    {
        if (array is null)
            return null;

        var length_0 = array.GetLength(0);
        var length_1 = array.GetLength(1);
        var result = new T[length_0, length_1];
        for (var i_0 = 0; i_0 < length_0; i_0++)
        {
            for (var i_1 = 0; i_1 < length_1; i_1++)
            {
                result[i_0, i_1] = array[i_0, i_1];
            }
        }
        return result;
    }
}
