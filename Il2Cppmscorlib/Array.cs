using Il2CppInterop.Common;

namespace Il2CppSystem;

public abstract class Array : Object
{
    protected Array(ObjectPointer ptr) : base(ptr)
    {
    }

    public Int32 Length => default;

    public Int32 GetLowerBound(Int32 dimension)
    {
        return default;
    }

    public Int32 GetUpperBound(Int32 dimension)
    {
        return default;
    }

    public Int32 GetLength(Int32 dimension)
    {
        return default;
    }

    public Int32 GetRank()
    {
        return default;
    }
}
