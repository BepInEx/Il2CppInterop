using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Reflection;
using Il2CppInterop.Common.Attributes;
using Il2CppInterop.Runtime.Extensions;
using Il2CppInterop.Runtime.Structs;
using Il2CppInterop.Runtime.Structs.VersionSpecific.MethodInfo;

namespace Il2CppInterop.Runtime.Injection;

internal readonly record struct NamedSignatureHash
{
    private readonly UInt128 _hash;

    public unsafe NamedSignatureHash(INativeMethodInfoStruct methodInfo)
    {
        XxHash128 hash = new();
        hash.Append(methodInfo.GetNameSpan());
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
    public NamedSignatureHash(MethodInfo methodInfo)
    {
        XxHash128 hash = new();
        var name = methodInfo.GetCustomAttribute<Il2CppMethodAttribute>()?.Name ?? methodInfo.Name;
        hash.AppendUtf8(name);
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
