using System;
using System.Diagnostics.CodeAnalysis;
using System.IO.Hashing;
using System.Reflection;
using Il2CppInterop.Runtime.Extensions;

namespace Il2CppInterop.Runtime.Injection;

internal readonly record struct TrampolineSignatureHash
{
    private readonly UInt128 _hash;

    [RequiresDynamicCode("")]
    public TrampolineSignatureHash(MethodInfo methodInfo)
    {
        XxHash128 hash = new();
        hash.Append(methodInfo.IsStatic);
        hash.Append(TrampolineBuilder.GetNativeType(methodInfo.ReturnType).FullName);
        foreach (var parameter in methodInfo.GetParameters())
        {
            hash.Append(TrampolineBuilder.GetNativeType(parameter.ParameterType).FullName);
        }
        _hash = hash.GetCurrentHashAsUInt128();
    }

    public override string ToString() => _hash.ToString();
}
