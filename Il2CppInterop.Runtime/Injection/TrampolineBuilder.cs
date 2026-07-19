using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.Structs;
using Microsoft.Extensions.Logging;

namespace Il2CppInterop.Runtime.Injection;

[RequiresDynamicCode("")]
public static class TrampolineBuilder
{
    private static readonly AssemblyBuilder _fixedStructAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FixedSizeStructAssembly"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder _fixedStructModuleBuilder = _fixedStructAssembly.DefineDynamicModule("FixedSizeStructAssembly");
    private static readonly Dictionary<int, Type> _fixedStructCache = new();

    private static readonly AssemblyBuilder _delegateAssembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Il2CppTrampolineDelegates"), AssemblyBuilderAccess.Run);
    private static readonly ModuleBuilder _delegateModule = _delegateAssembly.DefineDynamicModule("Il2CppTrampolineDelegates");
    private static readonly ConcurrentDictionary<TrampolineSignatureHash, Type> ourDelegateTypes = new();

    private static Type GetFixedSizeStructType(int size)
    {
        if (_fixedStructCache.TryGetValue(size, out var result))
        {
            return result;
        }

        var tb = _fixedStructModuleBuilder.DefineType($"IL2CPPDetour_FixedSizeStruct_{size}b", TypeAttributes.SequentialLayout | TypeAttributes.BeforeFieldInit | TypeAttributes.Sealed, typeof(ValueType));
        tb.DefineField("_element0", typeof(byte), FieldAttributes.Private);

        // Apply InlineArray attribute
        var data = new byte[8];
        data[0] = 1;
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(2), size);
        tb.SetCustomAttribute(typeof(InlineArrayAttribute).GetConstructors()[0], data);

        var type = tb.CreateType();
        return _fixedStructCache[size] = type;
    }

    public static Type GetNativeType(Type managedType)
    {
        if (managedType == typeof(void))
        {
            return managedType;
        }
        else if (!managedType.IsValueType)
        {
            if (managedType.IsByRef)
            {
                throw new NotSupportedException("ByRef types are not supported in NativeType conversion.");
            }
            else if (managedType.IsArray || managedType.IsSZArray)
            {
                throw new NotSupportedException("Array types are not supported in NativeType conversion.");
            }

            // General reference type
            return typeof(IntPtr);
        }
        else if (managedType == typeof(Il2CppSystem.Boolean))
        {
            // bool is byte in Il2Cpp, but int in CLR => force size to be correct
            return typeof(byte);
        }
        else if (typeof(IByReference).IsAssignableFrom(managedType))
        {
            // ByReference types have no class, so we need this marker interface to identify them.
            return typeof(IntPtr);
        }
        else if (typeof(IPointer).IsAssignableFrom(managedType))
        {
            return typeof(IntPtr);
        }
        else
        {
            // Struct that's passed on the stack => handle as general struct

            var nativeClassPtr = Il2CppType.GetClassPointer(managedType);
            if (nativeClassPtr == IntPtr.Zero)
            {
                throw new NotSupportedException($"Type {managedType.FullName} is not an Il2Cpp type.");
            }

            var fixedSize = IL2CPP.il2cpp_class_value_size(nativeClassPtr, out _);
            return GetFixedSizeStructType(fixedSize);
        }
    }

    [RequiresUnreferencedCode("")]
    public static Type GetOrCreateDelegateType(MethodInfo monoMethod)
    {
        return ourDelegateTypes.GetOrAdd(new TrampolineSignatureHash(monoMethod), CreateDelegateType, monoMethod);
    }

    [RequiresUnreferencedCode("")]
    private static Type CreateDelegateType(TrampolineSignatureHash signatureHash, MethodInfo monoMethod)
    {
        var typeName = $"Il2CppToManagedDelegate_{signatureHash}";

        var newType = _delegateModule.DefineType(typeName, TypeAttributes.Sealed | TypeAttributes.Public,
            typeof(MulticastDelegate));

        newType.DefineConstructor(
            MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
            MethodAttributes.Public, CallingConventions.HasThis, [typeof(object), typeof(IntPtr)])
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        var parameterTypes = GetNativeParameters(monoMethod);

        newType.DefineMethod("Invoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            CallingConventions.HasThis,
            GetNativeType(monoMethod.ReturnType),
            parameterTypes)
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        newType.DefineMethod("BeginInvoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot |
            MethodAttributes.Public,
            CallingConventions.HasThis, typeof(IAsyncResult),
            parameterTypes.Concat([typeof(AsyncCallback), typeof(object)]).ToArray())
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        newType.DefineMethod("EndInvoke",
            MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot | MethodAttributes.Public,
            CallingConventions.HasThis,
            GetNativeType(monoMethod.ReturnType),
            [typeof(IAsyncResult)])
            .SetImplementationFlags(MethodImplAttributes.CodeTypeMask);

        return newType.CreateType();
    }

    private static Type[] GetNativeParameters(MethodInfo monoMethod) =>
    [
        ..(ReadOnlySpan<Type>)(monoMethod.IsStatic ? [] : [typeof(IntPtr)]),
        ..monoMethod.GetParameters().Select(it => GetNativeType(it.ParameterType)),
        typeof(Il2CppMethodInfo*),
    ];

    [RequiresUnreferencedCode("")]
    internal static Delegate CreateTrampoline(MethodInfo monoMethod, bool callVirt)
    {
        Debug.Assert(monoMethod.DeclaringType is not null);

        var nativeReturnType = GetNativeType(monoMethod.ReturnType);
        var nativeParameterTypes = GetNativeParameters(monoMethod);

        Type[] managedParameters =
        [
            ..(ReadOnlySpan<Type>)(monoMethod.IsStatic ? [] : [monoMethod.DeclaringType!]),
            ..monoMethod.GetParameters().Select(it => it.ParameterType),
        ];

        var trampoline = new DynamicMethod(
            $"Trampoline_{monoMethod.DeclaringType}_{monoMethod.Name}_{new NamedSignatureHash(monoMethod)}{(callVirt ? "_Virtual" : "")}",
            MethodAttributes.Static | MethodAttributes.Public, CallingConventions.Standard,
            nativeReturnType, nativeParameterTypes,
            typeof(TypeInjector), true);

        var delegateType = GetOrCreateDelegateType(monoMethod);

        var body = trampoline.GetILGenerator();

        body.BeginExceptionBlock();

        // Value types boxed as interfaces can be modified during the execution of the method.
        List<(LocalBuilder Local, int ArgumentIndex)> interfaceArguments = [];

        LocalBuilder? thisVariable = null;
        if (!monoMethod.IsStatic)
        {
            body.Emit(OpCodes.Ldarg_0);
            if (monoMethod.DeclaringType.IsValueType)
            {
                // Need to store the value in a local variable so that we can pass a reference to the managed method
                thisVariable = body.DeclareLocal(monoMethod.DeclaringType);
                body.Emit(OpCodes.Conv_U);
                body.Emit(OpCodes.Call, typeof(Il2CppType).GetMethod(nameof(Il2CppType.ReadFromPointer), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(monoMethod.DeclaringType));
                body.Emit(OpCodes.Stloc, thisVariable);
                body.Emit(OpCodes.Ldloca, thisVariable);
            }
            else
            {
                body.Emit(OpCodes.Call, typeof(Il2CppObjectPool).GetMethod(nameof(Il2CppObjectPool.Get))!);
                body.Emit(OpCodes.Castclass, monoMethod.DeclaringType);

                if (monoMethod.DeclaringType.IsInterface)
                {
                    var local = body.DeclareLocal(monoMethod.DeclaringType);
                    body.Emit(OpCodes.Stloc, local);
                    body.Emit(OpCodes.Ldloc, local);
                    interfaceArguments.Add((local, 0));
                }
            }
        }

        var argOffset = monoMethod.IsStatic ? 0 : 1;

        for (var i = argOffset; i < managedParameters.Length; i++)
        {
            body.Emit(OpCodes.Ldarg, i);
            body.Emit(OpCodes.Call, typeof(TypeInjector).GetMethod(nameof(ConvertNativeToManaged), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(nativeParameterTypes[i], managedParameters[i]));
            if (managedParameters[i].IsInterface)
            {
                var local = body.DeclareLocal(managedParameters[i]);
                body.Emit(OpCodes.Stloc, local);
                body.Emit(OpCodes.Ldloc, local);
                interfaceArguments.Add((local, i));
            }
        }

        body.Emit(callVirt ? OpCodes.Callvirt : OpCodes.Call, monoMethod);
        LocalBuilder? nativeReturnVariable = null;
        if (monoMethod.ReturnType != typeof(void))
        {
            nativeReturnVariable = body.DeclareLocal(nativeReturnType);
            body.Emit(OpCodes.Call, typeof(TypeInjector).GetMethod(nameof(ConvertManagedToNative), BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(monoMethod.ReturnType, nativeReturnType));
            body.Emit(OpCodes.Stloc, nativeReturnVariable);
        }

        if (thisVariable != null)
        {
            // Copy any changes to the value type back to the pointer passed by il2cpp
            Debug.Assert(monoMethod.DeclaringType.IsValueType);
            body.Emit(OpCodes.Ldloc, thisVariable);
            body.Emit(OpCodes.Ldarg_0);
            body.Emit(OpCodes.Conv_U);
            body.Emit(OpCodes.Call, typeof(Il2CppType).GetMethod(nameof(Il2CppType.WriteToPointer), BindingFlags.Public | BindingFlags.Static)!.MakeGenericMethod(monoMethod.DeclaringType));
        }

        // Update boxed value types passed as interfaces
        foreach ((var local, var argumentIndex) in interfaceArguments)
        {
            body.Emit(OpCodes.Ldarg, argumentIndex);
            body.Emit(OpCodes.Ldloc, local);
            body.Emit(OpCodes.Call, typeof(TypeInjector).GetMethod(nameof(UpdateBoxedValue), BindingFlags.Static | BindingFlags.NonPublic)!);
        }

        body.BeginCatchBlock(typeof(Exception));
        body.Emit(OpCodes.Call, typeof(TypeInjector).GetMethod(nameof(LogError), BindingFlags.Static | BindingFlags.NonPublic)!);

        body.EndExceptionBlock();

        if (nativeReturnVariable != null)
        {
            body.Emit(OpCodes.Ldloc, nativeReturnVariable);
        }

        body.Emit(OpCodes.Ret);

        return trampoline.CreateDelegate(delegateType);
    }

    private static void LogError(Exception exception)
    {
        Logger.Instance.LogError("Exception in IL2CPP-to-Managed trampoline, not passing it to il2cpp: {Exception}", exception);
    }

    private static unsafe void UpdateBoxedValue(IntPtr objectPointer, IIl2CppType? @object)
    {
        if (objectPointer == IntPtr.Zero || @object == null)
            return;

        if (!@object.GetType().IsValueType)
            return;

        var size = IL2CPP.il2cpp_class_value_size(@object.ObjectClass, out _);
        var sourceSpan = new ReadOnlySpan<byte>((void*)IL2CPP.il2cpp_object_unbox((nint)@object.BoxNative()), size);
        var destinationSpan = new Span<byte>((void*)IL2CPP.il2cpp_object_unbox(objectPointer), size);
        sourceSpan.CopyTo(destinationSpan);
    }

    private static unsafe TManaged? ConvertNativeToManaged<TNative, TManaged>(TNative value)
        where TNative : unmanaged
        where TManaged : IIl2CppType<TManaged>
    {
        var span = new ReadOnlySpan<byte>(&value, sizeof(TNative));
        return TManaged.ReadFromSpan(span);
    }

    private static unsafe TNative ConvertManagedToNative<TManaged, TNative>(TManaged? value)
        where TNative : unmanaged
        where TManaged : IIl2CppType<TManaged>
    {
        TNative result = default;
        var span = new Span<byte>(&result, sizeof(TNative));
        TManaged.WriteToSpan(value, span);
        return result;
    }
}
