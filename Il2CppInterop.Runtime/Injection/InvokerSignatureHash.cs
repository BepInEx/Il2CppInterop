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
        hash.AppendFullName(methodInfo.ReturnType);
        for (var i = 0; i < methodInfo.ParametersCount; i++)
        {
            var parameter = UnityVersionHandler.Wrap(methodInfo.Parameters, i);
            hash.AppendFullName(parameter.ParameterType);
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    [RequiresDynamicCode("")]
    public InvokerSignatureHash(MethodInfo methodInfo)
    {
        XxHash128 hash = new();
        hash.Append(methodInfo.IsStatic);
        hash.AppendFullName(methodInfo.ReturnType);
        foreach (var parameter in methodInfo.GetParameters())
        {
            hash.AppendFullName(parameter.ParameterType);
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    public override string ToString() => _hash.ToString();
}
