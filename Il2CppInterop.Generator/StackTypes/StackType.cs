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
}
