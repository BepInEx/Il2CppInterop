# Method Invokers

Every normal (user-facing) Il2Cpp method has an unsafe invoker.

* Invoker methods are static.
* Invoker methdos are public, so they can be called from other generated assemblies.
* If the Il2Cpp method is instance, the first parameter is the object.
* All other invoker parameter types are wrapped in `ByRef<>`, including parameters that are already `ByRef<>`.
* Similarly, local variable types in unstripped invokers are also wrapped in `ByRef<>`.
* The data for local variables is stack allocated at the beginning of the method.
* Unstripped invokers call other invokers, not the user-facing methods.

This is extremely verbose, but it preserves semantics exactly for indirect memory access of reference types.

## Example Output

```cs
// Original
public string GetString<T>(int param1, T param2, IInterface param3);

// Il2Cpp
public String GetString<T>(Int32 param1, T param2, IInterface param3) where T : IIl2CppType<T>
{
    // Value type
    void* obj = stackalloc byte[Il2CppTypeHelper.SizeOf<Self>()];
    Il2CppTypeHelper.WriteToPointer<Self>(this, obj);

    // Reference type
    void* obj = (void*)this.Pointer;

    // Param 1
    byte* data_param1 = stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()];
    Il2CppTypeHelper.WriteToPointer<Int32>(param1, data_param1);

    // Param 2
    byte* data_param2 = stackalloc byte[Il2CppTypeHelper.SizeOf<T>()];
    Il2CppTypeHelper.WriteToPointer<T>(param2, data_param2);

    // Param 3
    byte* data_param3 = stackalloc byte[Il2CppTypeHelper.SizeOf<IInterface>()];
    Il2CppTypeHelper.WriteToPointer<IInterface>(param3, data_param3);

    String result = Unsafe_GetString<T>(obj, new ByReference<Int32>(data_param1), new ByReference<T>(data_param2), new ByReference<IInterface>(data_param3));

    // Value type
    this = Il2CppTypeHelper.ReadFromPointer<Self>(obj);

    return result;
}

// Invoker
public static String Unsafe_GetString<T>(void* obj, ByReference<Int32> param1, ByReference<T> param2, ByReference<IInterface> param3) where T : IIl2CppType<T>
{
    void** arguments = (void**)(stackalloc IntPtr[3]);
    arguments[0] = param1.ToPointer();
    arguments[1] = param2.ToPointer();
    arguments[2] = param3.ToPointer();
    return RuntimeInvokeHelper.InvokeFunction<String>(/* method info */, (IntPtr)obj, arguments);
}

// Unstripped Original
public static bool Compare(Self param1, Self param2)
{
    string local1 = param1.GetString<long>(0, 0, null);
    string local2 = param2.GetString<long>(0, 0, null);
    return local1 == local2;
}

// Unstripped Invoker
public static Boolean Unsafe_Compare(ByReference<Self> param1, ByReference<Self> param2)
{
    ByReference<String> data_local1 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<String>()]);
    ByReference<String> data_local2 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<String>()]);

    ByReference<Int32> data_call1_param1 = new ByReference<Int32>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()]);
    ByReference<Int64> data_call1_param2 = new ByReference<Int64>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int64>()]);
    ByReference<IInterface> data_call1_param3 = new ByReference<IInterface>(stackalloc byte[Il2CppTypeHelper.SizeOf<IInterface>()]);

    ByReference<Int32> data_call2_param1 = new ByReference<Int32>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int32>()]);
    ByReference<Int64> data_call2_param2 = new ByReference<Int64>(stackalloc byte[Il2CppTypeHelper.SizeOf<Int64>()]);
    ByReference<IInterface> data_call2_param3 = new ByReference<IInterface>(stackalloc byte[Il2CppTypeHelper.SizeOf<IInterface>()]);

    ByReference<String> data_call3_param1 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<String>()]);
    ByReference<String> data_call3_param2 = new ByReference<String>(stackalloc byte[Il2CppTypeHelper.SizeOf<String>()]);

    // (ldarg or ldarga) param1 -> void* on stack
    // 0, 0L, and null also loaded onto the stack.
    // Arguments are converted from Mono to Il2Cpp and popped off 1 by 1 into the data.
    ByReference.SetValue<IInterface>((IInterface?)null, data_call1_param3);
    ByReference.SetValue<Int64>((Int64)0L, data_call1_param2);
    ByReference.SetValue<Int32>((Int32)0, data_call1_param1);
    String result_call1 = Unsafe_GetString<Int64>(/* object pointer from stack */, data_call1_param1, data_call1_param2, data_call1_param3);
    ByReference.SetValue<String>(result_call1, data_local1);

    // (ldarg or ldarga) param1 -> void* on stack
    // 0, 0L, and null also loaded onto the stack.
    // Arguments are converted from Mono to Il2Cpp and popped off 1 by 1 into the data.
    ByReference.SetValue<IInterface>((IInterface?)null, data_call2_param3);
    ByReference.SetValue<Int64>((Int64)0L, data_call2_param2);
    ByReference.SetValue<Int32>((Int32)0, data_call2_param1);
    String result_call2 = Unsafe_GetString<Int64>(/* object pointer from stack */, data_call2_param1, data_call2_param2, data_call2_param3);
    ByReference.SetValue<String>(result_call2, data_local2);

    // Locals 1 and 2 loaded onto the stack
    data_local1.GetValue()
    data_local2.GetValue()

    // Locals popped off the stack and stored in call data
    ByReference.SetValue<String>(/* from stack */, data_call3_param2);
    ByReference.SetValue<String>(/* from stack */, data_call3_param1);

    return String.Unsafe_op_Equality(data_call3_param1, data_call3_param2);
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

## Generated Helpers

```cs
public static unsafe TResult? InvokeInstanceFunction<TSelf, T0, T1, TResult>(ref TSelf obj, T0? arg0, T1? arg1, delegate* managed<void*, ByReference<T0>, ByReference<T1>, TResult> invoker)
    where TSelf : IIl2CppType<TSelf>
    where T0 : IIl2CppType<T0>
    where T1 : IIl2CppType<T1>
    where TResult : IIl2CppType<TResult>
{
    void* data_obj = stackalloc byte[Il2CppTypeHelper.SizeOf<TSelf>()];
    Il2CppTypeHelper.WriteToPointer<TSelf>(obj, data_obj);

    // Param 0
    byte* data_param0 = stackalloc byte[Il2CppTypeHelper.SizeOf<T0>()];
    Il2CppTypeHelper.WriteToPointer<T0>(arg0, data_param0);

    // Param 1
    byte* data_param1 = stackalloc byte[Il2CppTypeHelper.SizeOf<T1>()];
    Il2CppTypeHelper.WriteToPointer<T1>(arg1, data_param1);

    TResult? result = invoker(data_obj, new(data_param0), new(data_param1));

    if (RuntimeInvokeHelper.IsValueType<TSelf>())
    {
        obj = Il2CppTypeHelper.ReadFromPointer<TSelf>(data_obj)!;
    }

    return result;
}
```

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
