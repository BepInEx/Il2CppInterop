using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using Il2CppInterop.Runtime.InteropTypes;

namespace Il2CppInterop.Runtime.Injection;

internal static class TrampolineHelpers
{
    private static AssemblyBuilder _fixedStructAssembly;
    private static ModuleBuilder _fixedStructModuleBuilder;
    private static readonly Dictionary<int, Type> _fixedStructCache = new();

    private static Type GetFixedSizeStructType(int size)
    {
        if (_fixedStructCache.TryGetValue(size, out var result))
        {
            return result;
        }

        _fixedStructAssembly ??= AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("FixedSizeStructAssembly"), AssemblyBuilderAccess.Run);
        _fixedStructModuleBuilder ??= _fixedStructAssembly.DefineDynamicModule("FixedSizeStructAssembly");

        var tb = _fixedStructModuleBuilder.DefineType($"IL2CPPDetour_FixedSizeStruct_{size}b", TypeAttributes.ExplicitLayout, typeof(ValueType), size);

        var type = tb.CreateType();
        return _fixedStructCache[size] = type;
    }

    internal static bool IsArm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

    // AAPCS64 passes composites of up to 16 bytes in registers and copies larger ones to memory, passing a pointer
    private const int Arm64MaxRegisterStructSize = 16;

    internal static int GetValueTypeSize(Type managedType)
    {
        uint align = 0;
        return IL2CPP.il2cpp_class_value_size(Il2CppClassPointerStore.GetNativeClassPointer(managedType), ref align);
    }

    /// <summary>
    /// Whether native code passes a by-value struct wrapper (a subclass of Il2CppSystem.ValueType) as the struct's bytes,
    /// rather than as a pointer to them
    /// </summary>
    internal static bool IsStructWrapperPassedByValue(Type managedType)
    {
        // x86 passes structs on the stack
        if (!Environment.Is64BitProcess)
            return true;

        if (IsArm64)
            return GetValueTypeSize(managedType) <= Arm64MaxRegisterStructSize;

        // Win64 passes structs that aren't 1, 2, 4 or 8 bytes by reference, and IL2CPP's struct wrappers are treated that way
        return false;
    }

    /// <summary>
    /// The type native code returns for a managed return type. On ARM64 a struct wrapper is returned as its bytes:
    /// up to 16 bytes come back in x0/x1, and larger ones go through the buffer the caller passes in x8, which CoreCLR
    /// fills in itself when the method's return type is a struct of that size
    /// </summary>
    internal static Type NativeReturnType(this Type managedType)
    {
        if (IsArm64 && managedType.IsSubclassOf(typeof(Il2CppSystem.ValueType)))
            return GetFixedSizeStructType(GetValueTypeSize(managedType));

        return managedType.NativeType();
    }

    internal static Type NativeType(this Type managedType)
    {
        if (managedType.IsByRef)
        {
            var directType = managedType.GetElementType();

            // bool is byte in Il2Cpp, but int in CLR => force size to be correct
            if (directType == typeof(bool))
            {
                return typeof(byte).MakeByRefType();
            }

            if (directType == typeof(string) || directType.IsSubclassOf(typeof(Il2CppObjectBase)))
            {
                return typeof(IntPtr*);
            }
        }
        else if (managedType.IsSubclassOf(typeof(Il2CppSystem.ValueType)) && IsStructWrapperPassedByValue(managedType))
        {
            // Struct that's passed on the stack or in registers => handle as general struct
            return GetFixedSizeStructType(GetValueTypeSize(managedType));
        }
        else if (managedType == typeof(string) || managedType.IsSubclassOf(typeof(Il2CppObjectBase))) // General reference type
        {
            return typeof(IntPtr);
        }
        else if (managedType == typeof(bool))
        {
            // bool is byte in Il2Cpp, but int in CLR => force size to be correct
            return typeof(byte);
        }

        return managedType;
    }
}
