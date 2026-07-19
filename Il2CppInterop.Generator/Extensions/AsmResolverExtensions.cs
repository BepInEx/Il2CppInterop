using AsmResolver.DotNet;

namespace Il2CppInterop.Generator.Extensions;

internal static class AsmResolverExtensions
{
    extension(CilOpCode opCode)
    {
        public bool IsLoadConstantI4
        {
            get => opCode.Code is (>= CilCode.Ldc_I4_M1 and <= CilCode.Ldc_I4_8) or CilCode.Ldc_I4_S or CilCode.Ldc_I4;
        }
    }

    public static TypeDefinition? TryResolve(this ITypeDescriptor typeDescriptor, RuntimeContext? runtimeContext)
    {
        return typeDescriptor.TryResolve(runtimeContext, out var resolvedType) ? resolvedType : null;
    }
}
