# Method Invokers

Every normal (user-facing) Il2Cpp method has an unsafe implementation.

* Implementation methods are static.
* Implementation methods are private, so they can only be called from within the type.
* If the Il2Cpp method is instance, the first parameter is the object.
* All other implementation parameter types are wrapped in `ByReference<>`, including parameters that are already `ByReference<>`.
* Similarly, local variable types are also wrapped in `ByReference<>`.
* The data for local variables is stack allocated at the beginning of the method.

Some methods have an unsafe invoker.

* Invoker methods are static.
* Invoker methods are public, so they can be called from other generated assemblies where desirable.

This can be verbose, but it preserves semantics exactly for indirect memory access of reference types.

## Example Output

```cs
// Original
public string GetString<T>(int param1, T param2, IInterface param3);

// Il2Cpp
public String GetString<T>(Int32 param1, T param2, IInterface param3) where T : IIl2CppType<T>
{
    ByReference<Self> data_this = new ByReference<Self>(stackalloc byte[Il2CppTypeHelper.SizeOf<Self>()]);

    // Value type
    data_this.CopyFrom(this);

    // Reference type
    data_this.SetValue(this);

    String result = UnsafeInvoke_GetString<T>(data_this, param1, param2, param3);

    // Value type
    data_this.CopyTo(this);

    return result;
}

// Invoker
public static String UnsafeInvoke_GetString<T>(ByReference<Self> @this, Int32 param1, T param2, IInterface param3) where T : IIl2CppType<T>
{
    // Param 1
    ByReference<Int32> data_param1 = new ByReference<Int32>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()]);
    data_param1.SetValue(param1);

    // Param 2
    ByReference<T> data_param2 = new ByReference<T>(stackalloc byte[Il2CppTypeHelper.SizeOf<T>()]);
    data_param2.SetValue(param2);

    // Param 3
    ByReference<IInterface> data_param3 = new ByReference<IInterface>(stackalloc byte[Il2CppTypeHelper.SizeOf<IInterface>()]);
    data_param3.SetValue(param3);

    return UnsafeImplementation_GetString<T>(@this, data_param1, data_param2, data_param3);
}
private static String UnsafeImplementation_GetString<T>(ByReference<Self> @this, ByReference<Int32> param1, ByReference<T> param2, ByReference<IInterface> param3) where T : IIl2CppType<T>
{
    IntPtr* arguments = stackalloc IntPtr[3];
    arguments[0] = RuntimeInvokeHelper.GetPointerForParameter(param1);
    arguments[1] = RuntimeInvokeHelper.GetPointerForParameter(param2);
    arguments[2] = RuntimeInvokeHelper.GetPointerForParameter(param3);
    return RuntimeInvokeHelper.InvokeFunction<String>(/* method info */, RuntimeInvokeHelper.GetPointerForThis<Self>(@this), (void**)arguments);
}

// Unstripped Original
public static bool Compare(Self param1, Self param2)
{
    string local1 = param1.GetString<long>(0, 0, null);
    string local2 = param2.GetString<long>(0, 0, null);
    return local1 == local2;
}

// Unstripped Invoker
public static Boolean Compare(Self param1, Self param2)
{
    ByReference<Self> data_param1 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<Self>()]);
    data_param1.SetValue(param1);
    ByReference<Self> data_param2 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<Self>()]);
    data_param1.SetValue(param2);

    return UnsafeImplementation_Compare(data_param1, data_param2)
}
private static Boolean UnsafeImplementation_Compare(ByReference<Self> param1, ByReference<Self> param2)
{
    ByReference<String> data_local1 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<String>()]);
    ByReference<String> data_local2 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<String>()]);

    data_local1.SetValue(param1.GetValue().GetString<Int64>((Int32)0, (Int64)0, (IInterface?)null));
    data_local2.SetValue(param2.GetValue().GetString<Int64>((Int32)0, (Int64)0, (IInterface?)null));

    return data_local1.GetValue() == data_local2.GetValue();
}
```

## Unstripped Instruction Translations

### ldind_Ref

* ldind_I
* `Il2CppObjectPool.Get`

### stind_Ref

* `Box`
* stind_I

### ldarga

* ldarg
* `ByReference<T>.ToPointer()`

### ldarg

* ldarg
* `ByReference<T>.GetValue()`

### starg

* ldarg
* `ByReference.SetValue<T>`

### ldloca

* ldloc
* `ByReference<T>.ToPointer()`

### ldloc

* ldloc
* `ByReference<T>.GetValue()`

### stloc

* ldloc
* `ByReference.SetValue<T>`

### call / callvirt

Since argument data is handled by the caller, additional locals need to be created to store those arguments.

* Arguments are converted from Mono to Il2Cpp and popped off 1 by 1 into the data.
* Data variables are loaded onto the stack.
* Call the target's invoker method

## `ref` parameters

```cs
// Il2Cpp method
public ByReference<Int32> Method(Int32 param_normal, [In] ByReference<Int32> param_in, ByReference<Int32> param_ref, [Out] ByReference<Int32> param_out);

// Injected overload
public ByReference<Int32> Method(Int32 param_normal, in Int32 param_in, ref Int32 param_ref, out Int32 param_out)
{
    ByReference<Int32> data_param_in = new ByReference<Int32>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()]);
    data_param_in.CopyFrom(in param_in);

    ByReference<Int32> data_param_ref = new ByReference<Int32>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()]);
    data_param_ref.CopyFrom(ref param_ref);

    ByReference<Int32> data_param_out = new ByReference<Int32>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()]);
    data_param_out.Clear();

    ByReference<Int32> result = Method(param_normal, data_param_in, data_param_ref, data_param_out);

    data_param_ref.CopyTo(out param_ref);
    data_param_out.CopyTo(out param_out);

    return result;
}
```

Return types are unchanged for two reasons:

* There's no way to convert `ByReference<T>` to a managed reference for all type parameters.
* Overloads can't differ only on the return type.
