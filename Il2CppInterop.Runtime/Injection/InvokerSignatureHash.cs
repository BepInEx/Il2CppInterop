using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Reflection;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Structs;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;

namespace Il2CppInterop.Runtime.Injection;

/// <summary>
/// A hash representing the signature of a generated invoker method
/// </summary>
internal readonly record struct InvokerSignatureHash
{
    private readonly UInt128 _hash;

    public unsafe InvokerSignatureHash(INativeMethodInfoStruct methodInfo)
    {
        XxHash128 hash = new();
        hash.Append((methodInfo.Flags & Il2CppMethodFlags.METHOD_ATTRIBUTE_STATIC) != 0);
        hash.Append(GetFullName(methodInfo.ReturnType));
        for (var i = 0; i < methodInfo.ParametersCount; i++)
        {
            var parameter = UnityVersionHandler.Wrap(methodInfo.Parameters, i);
            hash.Append(GetFullName(parameter.ParameterType));
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    [RequiresDynamicCode("")]
    public InvokerSignatureHash(MethodInfo methodInfo)
    {
        XxHash128 hash = new();
        hash.Append(methodInfo.IsStatic);
        hash.Append(GetFullName(methodInfo.ReturnType));
        foreach (var parameter in methodInfo.GetParameters())
        {
            hash.Append(GetFullName(parameter.ParameterType));
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    [RequiresDynamicCode("")]
    private static unsafe Il2CppSystem.String GetFullName(Type type)
    {
        return GetFullName((Il2CppTypeStruct*)Il2CppTypePointerStore.GetNativeTypePointer(type));
    }

    private static unsafe Il2CppSystem.String GetFullName(Il2CppTypeStruct* type)
    {
        return Il2CppSystem.Type.FromTypePointer((nint)type).FullName;
    }

    public override string ToString() => _hash.ToString();
}
