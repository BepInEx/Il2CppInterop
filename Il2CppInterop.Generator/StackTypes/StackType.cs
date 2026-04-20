namespace Il2CppInterop.Generator.StackTypes;

public abstract record class StackType
{
    public static StackType Merge(StackType a, StackType b)
    {
        if (a is IncompatibleStackType || b is IncompatibleStackType)
        {
            return IncompatibleStackType.Instance;
        }
        if (a is UnknownStackType)
        {
            return b;
        }
        if (b is UnknownStackType)
        {
            return a;
        }
        if (EqualityComparer<StackType>.Default.Equals(a, b))
        {
            return a;
        }

        // Could be improved, but good enough for now.
        return IncompatibleStackType.Instance;
    }

    public static StackType MergeForMathOperation(StackType a, StackType b)
    {
        if (a is IntegerStackType64 && b is IntegerStackType)
        {
            return IntegerStackType64.Instance;
        }
        if (b is IntegerStackType64 && a is IntegerStackType)
        {
            return IntegerStackType64.Instance;
        }
        if (a is IntegerStackTypeNative && b is IntegerStackType32)
        {
            return IntegerStackTypeNative.Instance;
        }
        if (b is IntegerStackTypeNative && a is IntegerStackType32)
        {
            return IntegerStackTypeNative.Instance;
        }
        return Merge(a, b);
    }
}
