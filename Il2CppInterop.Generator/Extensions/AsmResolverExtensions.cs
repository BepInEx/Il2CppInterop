using AsmResolver.PE.DotNet.Cil;

namespace Il2CppInterop.Generator.Extensions;

internal static class AsmResolverExtensions
{
    extension(OpCode opCode)
    {
        public bool IsLoadConstantI4
        {
            get => opCode.Code is (>= CilCode.Ldc_I4_M1 and <= CilCode.Ldc_I4_8) or CilCode.Ldc_I4_S or CilCode.Ldc_I4;
        }
    }
}
