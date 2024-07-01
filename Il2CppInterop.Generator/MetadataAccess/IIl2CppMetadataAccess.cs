using AsmResolver.DotNet;
using AsmResolver.DotNet.Signatures;

namespace Il2CppInterop.Generator.MetadataAccess;

public interface IIl2CppMetadataAccess : IMetadataAccess
{
    IList<GenericInstanceTypeSignature>? GetKnownInstantiationsFor(TypeDefinition genericDeclaration);
    string? GetStringStoredAtAddress(long offsetInMemory);
    MemberReference? GetMethodRefStoredAt(long offsetInMemory);
}
