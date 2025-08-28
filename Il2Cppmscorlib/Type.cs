using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public abstract class Type : Object
{
    public RuntimeTypeHandle _impl { get; set; }

    public abstract Type GetNestedType(String name, BindingFlags bindingAttr);

    public static Type internal_from_handle(IntPtr handle)
    {
        throw null;
    }

    public virtual RuntimeTypeHandle TypeHandle
    {
        get
        {
            throw null;
        }
    }

    public Boolean IsPrimitive
    {
        get
        {
            throw null;
        }
    }

    public Boolean IsByRef
    {
        get
        {
            throw null;
        }
    }

    public MethodInfo GetMethod(String name)
    {
        throw null;
    }

    public abstract String FullName { get; }

    public Type MakeGenericType(Type[] typeArguments) => throw null;
}
