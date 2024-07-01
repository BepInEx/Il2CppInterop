using AsmResolver;
using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.Utils;
internal static class ReferenceCreator
{
    public static MemberReference CreateFieldReference(Utf8String? name, TypeSignature fieldType, IMemberRefParent? parent)
    {
        return new MemberReference(parent, name, new FieldSignature(fieldType));
    }

    public static MemberReference CreateInstanceMethodReference(Utf8String? name, TypeSignature returnType, IMemberRefParent? parent, params TypeSignature[] parameterTypes)
    {
        return new MemberReference(parent, name, MethodSignature.CreateInstance(returnType, parameterTypes));
    }

    public static MemberReference CreateStaticMethodReference(Utf8String? name, TypeSignature returnType, IMemberRefParent? parent, params TypeSignature[] parameterTypes)
    {
        return new MemberReference(parent, name, MethodSignature.CreateStatic(returnType, parameterTypes));
    }
}
