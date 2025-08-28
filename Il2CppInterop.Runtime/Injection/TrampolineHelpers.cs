using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
        else if (managedType.IsSubclassOf(typeof(Il2CppSystem.ValueType)) && !Environment.Is64BitProcess)
        {
            // Struct that's passed on the stack => handle as general struct
            var fixedSize = IL2CPP.GetIl2cppValueSize(Il2CppClassPointerStore.GetNativeClassPointer(managedType));
            return GetFixedSizeStructType(fixedSize);
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
