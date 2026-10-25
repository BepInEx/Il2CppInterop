using Il2CppInterop.Common;
using Il2CppSystem.Reflection;

namespace Il2CppSystem;

public abstract class Type : MemberInfo, IIl2CppType<Type>
{
    static int IIl2CppType<Type>.Size => throw null;
    nint IIl2CppType.ObjectClass => throw null;
    static Type IIl2CppType<Type>.ReadFromSpan(System.ReadOnlySpan<byte> span) => throw null;
    static void IIl2CppType<Type>.WriteToSpan(Type value, System.Span<byte> span) => throw null;

    public RuntimeTypeHandle _impl { get; set; }

    public Assembly Assembly => throw null;

    public Boolean ContainsGenericParameters => throw null;

    public abstract String FullName { get; }

    public Boolean HasElementType => throw null;

    public Boolean IsArray => throw null;

    public Boolean IsByRef => throw null;

    public Boolean IsConstructedGenericType => throw null;

    public Boolean IsGenericParameter => throw null;

    public Boolean IsGenericType => throw null;

    public Boolean IsGenericTypeDefinition => throw null;

    public Boolean IsNested => throw null;

    public Boolean IsPointer => throw null;

    public Boolean IsPrimitive => throw null;

    public Boolean IsSZArray => throw null;

    public Boolean IsTypeDefinition => !HasElementType && !IsConstructedGenericType && !IsGenericParameter;

    public String Namespace => throw null;

    public virtual RuntimeTypeHandle TypeHandle => throw null;

    public Int32 GetArrayRank() => throw null;

    public Type GetElementType() => throw null;

    public Type GetGenericTypeDefinition() => throw null;

    public MethodInfo GetMethod(String name) => throw null;

    public abstract Type GetNestedType(String name, BindingFlags bindingAttr);

    public static Type internal_from_handle(IntPtr handle) => throw null;

    public Type MakeByRefType() => throw null;

    public Type MakeGenericType(Type[] typeArguments) => throw null;

    public Type MakePointerType() => throw null;

    public static Boolean operator ==(Type left, Type right) => throw null;
    public static Boolean operator !=(Type left, Type right) => throw null;

    public override bool Equals(object obj) => throw null;
    public override int GetHashCode() => throw null;
}
