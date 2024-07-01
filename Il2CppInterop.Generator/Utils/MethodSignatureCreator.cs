using AsmResolver.DotNet.Signatures;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace Il2CppInterop.Generator.Utils;

internal static class MethodSignatureCreator
{
    public static MethodSignature CreateMethodSignature(MethodAttributes attributes, TypeSignature returnType, params TypeSignature[] parameterTypes)
    {
        return CreateMethodSignature((attributes & MethodAttributes.Static) != 0, returnType, parameterTypes);
    }

    public static MethodSignature CreateMethodSignature(bool isStatic, TypeSignature returnType, params TypeSignature[] parameterTypes)
    {
        return isStatic ? MethodSignature.CreateStatic(returnType, parameterTypes) : MethodSignature.CreateInstance(returnType, parameterTypes);
    }
}
