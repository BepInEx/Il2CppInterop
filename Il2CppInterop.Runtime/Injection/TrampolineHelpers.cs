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

    private static readonly Type[] _precompiledFixedStructs =
    {
        typeof(FixedSizeStructs.Size1), typeof(FixedSizeStructs.Size2), typeof(FixedSizeStructs.Size3),
        typeof(FixedSizeStructs.Size4), typeof(FixedSizeStructs.Size5), typeof(FixedSizeStructs.Size6),
        typeof(FixedSizeStructs.Size7), typeof(FixedSizeStructs.Size8), typeof(FixedSizeStructs.Size9),
        typeof(FixedSizeStructs.Size10), typeof(FixedSizeStructs.Size11), typeof(FixedSizeStructs.Size12),
        typeof(FixedSizeStructs.Size13), typeof(FixedSizeStructs.Size14), typeof(FixedSizeStructs.Size15),
        typeof(FixedSizeStructs.Size16),
    };

    private static Type GetFixedSizeStructType(int size)
    {
        // Harmony builds the trampoline's delegate type in a separate in-memory assembly, which can only reference
        // these types by assembly name. That works for this assembly but not for a run-only dynamic one
        if (size >= 1 && size <= _precompiledFixedStructs.Length)
        {
            return _precompiledFixedStructs[size - 1];
        }

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

/// <summary>
/// Opaque structs of a given size, used by generated trampolines to receive and return IL2CPP structs by value.
/// Not meant to be used directly
/// </summary>
public static class FixedSizeStructs
{
    [StructLayout(LayoutKind.Sequential, Size = 1)] public struct Size1 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 2)] public struct Size2 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 3)] public struct Size3 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 4)] public struct Size4 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 5)] public struct Size5 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 6)] public struct Size6 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 7)] public struct Size7 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 8)] public struct Size8 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 9)] public struct Size9 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 10)] public struct Size10 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 11)] public struct Size11 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 12)] public struct Size12 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 13)] public struct Size13 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 14)] public struct Size14 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 15)] public struct Size15 { private byte _; }
    [StructLayout(LayoutKind.Sequential, Size = 16)] public struct Size16 { private byte _; }
}
