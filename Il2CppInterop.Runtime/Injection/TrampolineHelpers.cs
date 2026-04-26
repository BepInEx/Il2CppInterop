using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Il2CppInterop.Runtime.InteropTypes;

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
        else if (typeof(IByReference).IsAssignableFrom(managedType))
        {
            // ByReference types have no class, so we need this marker interface to identify them.
            return typeof(IntPtr);
        }
        else
        {
            var nativeClassPtr = Il2CppClassPointerStore.GetNativeClassPointer(managedType);
            if (nativeClassPtr == IntPtr.Zero)
            {
                throw new NotSupportedException($"Type {managedType.FullName} is not an Il2Cpp type.");
            }

            if (IL2CPP.il2cpp_class_is_valuetype(nativeClassPtr))
            {
                // Struct that's passed on the stack => handle as general struct
                var fixedSize = IL2CPP.GetIl2cppValueSize(nativeClassPtr);
                return GetFixedSizeStructType(fixedSize);
            }
            else
            {
                // General reference type
                return typeof(IntPtr);
            }
        }
    }
}
