using System;
using Il2CppInterop.Runtime.Structs;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Class;
using Il2CppInterop.Runtime.Structs.VersionSpecific.Type;

namespace Il2CppInterop.Runtime.Injection;

internal static unsafe class GenericTypeInflater
{
    internal static nint InflateGenericType(nint genericClassPointer, nint[] genericArgClassPtrs)
    {
        return InflateGenericType(UnityVersionHandler.Wrap((Il2CppClass*)genericClassPointer), genericArgClassPtrs);
    }

    private static nint InflateGenericType(INativeClassStruct genericClassPointer, nint[] genericArgClassPtrs)
    {
        // This is an extremely naive implementation of generic type inflation and likely breaks the moment someone touches an inflated type.

        var inflatedClassPointer = UnityVersionHandler.NewClass(genericClassPointer.VTableCount);

        CopyFrom(inflatedClassPointer.ByValArg, genericClassPointer.ByValArg);
        CopyFrom(inflatedClassPointer.ThisArg, genericClassPointer.ThisArg);

        inflatedClassPointer.InstanceSize = genericClassPointer.InstanceSize;
        inflatedClassPointer.VTableCount = genericClassPointer.VTableCount;
        inflatedClassPointer.InterfaceCount = genericClassPointer.InterfaceCount;
        inflatedClassPointer.InterfaceOffsetsCount = genericClassPointer.InterfaceOffsetsCount;
        inflatedClassPointer.TypeHierarchyDepth = genericClassPointer.TypeHierarchyDepth;
        inflatedClassPointer.NativeSize = genericClassPointer.NativeSize;
        inflatedClassPointer.ActualSize = genericClassPointer.ActualSize;
        inflatedClassPointer.MethodCount = genericClassPointer.MethodCount;
        inflatedClassPointer.FieldCount = genericClassPointer.FieldCount;
        inflatedClassPointer.Flags = genericClassPointer.Flags;
        inflatedClassPointer.Name = genericClassPointer.Name;
        inflatedClassPointer.Namespace = genericClassPointer.Namespace;
        inflatedClassPointer.Image = genericClassPointer.Image;
        inflatedClassPointer.Parent = genericClassPointer.Parent;
        inflatedClassPointer.ElementClass = genericClassPointer.ElementClass;
        inflatedClassPointer.CastClass = genericClassPointer.CastClass;
        inflatedClassPointer.DeclaringType = genericClassPointer.DeclaringType;
        inflatedClassPointer.Class = inflatedClassPointer.Class;
        inflatedClassPointer.Fields = genericClassPointer.Fields;
        inflatedClassPointer.Methods = genericClassPointer.Methods;
        if (inflatedClassPointer.HasCombinedInterfaceData)
        {
            inflatedClassPointer.Interfaces = genericClassPointer.Interfaces;
        }
        else
        {
            inflatedClassPointer.ImplementedInterfaces = genericClassPointer.ImplementedInterfaces;
            inflatedClassPointer.InterfaceOffsets = genericClassPointer.InterfaceOffsets;
        }
        inflatedClassPointer.TypeHierarchy = genericClassPointer.TypeHierarchy;
        inflatedClassPointer.ValueType = genericClassPointer.ValueType;
        inflatedClassPointer.Initialized = true;
        inflatedClassPointer.EnumType = genericClassPointer.EnumType;
        inflatedClassPointer.IsGeneric = true;
        inflatedClassPointer.HasReferences = genericClassPointer.HasReferences;
        inflatedClassPointer.SizeInited = true;
        inflatedClassPointer.HasFinalize = genericClassPointer.HasFinalize;
        inflatedClassPointer.IsVtableInitialized = true;
        inflatedClassPointer.InitializedAndNoError = true;

        new ReadOnlySpan<VirtualInvokeData>(genericClassPointer.VTable, genericClassPointer.VTableCount)
            .CopyTo(new Span<VirtualInvokeData>(inflatedClassPointer.VTable, inflatedClassPointer.VTableCount));

        return inflatedClassPointer.Pointer;
    }

    private static void CopyFrom(INativeTypeStruct destination, INativeTypeStruct source)
    {
        destination.Data = source.Data;
        destination.Attrs = source.Attrs;
        destination.Type = source.Type;
        destination.ByRef = source.ByRef;
        destination.Pinned = source.Pinned;
        destination.ValueType = source.ValueType;
    }
}
