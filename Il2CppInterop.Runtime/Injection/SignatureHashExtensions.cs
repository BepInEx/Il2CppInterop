using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using Il2CppInterop.Common;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Structs;

namespace Il2CppInterop.Runtime.Injection;

/// <summary>
/// Appends the full names of the types in a method signature to the signature's hash.
/// </summary>
internal static class SignatureHashExtensions
{
    [RequiresDynamicCode("")]
    public static unsafe void AppendFullName(this XxHash128 hash, Type type)
    {
        hash.AppendFullName((Il2CppTypeStruct*)Il2CppTypePointerStore.GetNativeTypePointer(type));
    }

    public static unsafe void AppendFullName(this XxHash128 hash, Il2CppTypeStruct* type)
    {
        // Il2Cpp's own names are not used: it may name an injected class from metadata that the class does not have, and its
        // Type.FullName fails for some generic instances.
        var wrappedType = UnityVersionHandler.Wrap(type);
        switch (wrappedType.Type)
        {
            case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
                hash.AppendFullName((Il2CppTypeStruct*)wrappedType.Data);
                hash.Append("[]".AsSpan());
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
                var arrayType = (Il2CppArrayType*)wrappedType.Data;
                hash.AppendFullName(arrayType->etype);
                hash.Append($"[{new string(',', arrayType->rank - 1)}]".AsSpan());
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
                hash.AppendFullName((Il2CppTypeStruct*)wrappedType.Data);
                hash.Append("*".AsSpan());
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
                AppendClassName(hash, (Il2CppClass*)IL2CPP.il2cpp_class_from_type((nint)type));
                var typeArguments = GetTypeArguments(wrappedType.Data);
                hash.Append("[".AsSpan());
                for (var i = 0; i < typeArguments->type_argc; i++)
                {
                    hash.Append((i == 0 ? "[" : ",[").AsSpan());
                    hash.AppendFullName(typeArguments->type_argv[i]);
                    hash.Append("]".AsSpan());
                }
                hash.Append("]".AsSpan());
                break;
            case Il2CppTypeEnum.IL2CPP_TYPE_CLASS or Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE when TokenAllocator.TryGetClassPointer(wrappedType.Data, out var classPointer):
                AppendClassName(hash, (Il2CppClass*)classPointer);
                break;
            default:
                AppendClassName(hash, (Il2CppClass*)IL2CPP.il2cpp_class_from_type((nint)type));
                break;
        }
        if (wrappedType.ByRef)
            hash.Append("&".AsSpan());
    }

    private static unsafe Il2CppGenericInst* GetTypeArguments(nint genericClass)
    {
        // Il2CppGenericClass differs between versions, but context.class_inst always follows its first field.
        return ((Il2CppGenericClass*)genericClass)->context.class_inst;
    }

    private static unsafe void AppendClassName(XxHash128 hash, Il2CppClass* classPointer)
    {
        var classStruct = UnityVersionHandler.Wrap(classPointer);
        if (classStruct.DeclaringType != null)
        {
            AppendClassName(hash, classStruct.DeclaringType);
            hash.Append("+".AsSpan());
        }
        else if (Marshal.PtrToStringUTF8(classStruct.Namespace) is { Length: > 0 } @namespace)
        {
            hash.Append(@namespace.AsSpan());
            hash.Append(".".AsSpan());
        }
        hash.Append(Marshal.PtrToStringUTF8(classStruct.Name).AsSpan());
    }
}
