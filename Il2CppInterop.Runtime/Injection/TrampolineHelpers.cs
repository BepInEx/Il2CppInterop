using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;

namespace Il2CppInterop.Runtime.Injection;

internal static class TrampolineHelpers
{
    private static AssemblyBuilder? _fixedStructAssembly;
    private static ModuleBuilder? _fixedStructModuleBuilder;
    private static readonly Dictionary<int, Type> _fixedStructCache = new();

    internal static Type GetFixedSizeStructType(int size)
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
        if (managedType == typeof(void))
        {
            return managedType;
        }
        else if (managedType.IsByRef)
        {
            throw new NotSupportedException("ByRef types are not supported in NativeType conversion.");
        }
        else if (managedType.IsArray || managedType.IsSZArray)
        {
            throw new NotSupportedException("Array types are not supported in NativeType conversion.");
        }
        else if (managedType == typeof(Il2CppSystem.Boolean))
        {
            // bool is byte in Il2Cpp, but int in CLR => force size to be correct
            return typeof(byte);
        }
        else if (managedType.IsValueType)
        {
            // Struct that's passed on the stack => handle as general struct
            var fixedSize = IL2CPP.GetIl2cppValueSize(Il2CppClassPointerStore.GetNativeClassPointer(managedType));
            return GetFixedSizeStructType(fixedSize);
        }
        else
        {
            // General reference type
            return typeof(IntPtr);
        }
    }
}
